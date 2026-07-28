# Tasks: Logging and Command Interception

**Input**: `docs/specs/001-logging-interception/`
**Prerequisites**: `plan.md` (required), `spec.md` (required)

---

## Phase 1: Setup

- T001: Create folder structure (`Interceptors/`, `Diagnostics/`, `Configuration/`)
- T002: [P] Add package references (Microsoft.Extensions.Logging.Abstractions, System.Diagnostics.DiagnosticSource)
- T003: [P] Create unit test project structure for interceptors and diagnostics

---

## Phase 2: Foundational (Blocks User Stories)

**Goal**: Core infrastructure for interception

- T004: Create `CommandContext` record/class with SQL, parameters, connection, elapsed, exception
- T005: Define `ICommandInterceptor` interface with async methods
- T006: [P] Implement `InterceptorPipeline` to orchestrate multiple interceptors
- T007: [P] Add `InterceptorPipeline` registration to `JauntyConfig`

**Checkpoint**: Foundation complete - interceptor infrastructure ready

---

## Phase 3: User Story 1 - ILogger Logging (P1) MVP

**Goal**: Full Microsoft.Extensions.Logging integration

- T008: Create `LoggingConfiguration` class (LogLevel, SlowQueryThreshold, SensitiveParameterNames)
- T009: Implement `LoggingInterceptor` that wraps ILogger and handles slow query detection
- T010: [P] Create `JauntyLoggingExtensions` for IServiceCollection registration
- T011: Write unit tests for `LoggingInterceptor` (log levels, slow query, parameter masking)
- T012: Write integration tests verifying log output with real queries
- T013: Add XML documentation and usage examples

**Checkpoint**: US1 complete + tested - ILogger integration working

---

## Phase 4: User Story 2 - Command Interception (P1) MVP

**Goal**: Custom interceptor support for cross-cutting concerns

- T014: Ensure `InterceptorPipeline` invokes all registered interceptors in order
- T015: Implement short-circuit capability (exception from OnCommandExecuting)
- T016: Integrate `InterceptorPipeline` into `QueryCore` execution path
- T017: Integrate `InterceptorPipeline` into `ExecuteNonQueryCore` execution path
- T018: [P] Create sample `AuditInterceptor` for demonstration
- T019: Write unit tests for `InterceptorPipeline` (order, short-circuit, exception handling)
- T020: Write integration tests with custom interceptor
- T021: Add XML documentation and interceptor examples

**Checkpoint**: US2 complete + tested - Custom interceptors working

---

## Phase 5: User Story 3 - DiagnosticSource (P2)

**Goal**: DiagnosticSource event emission for observability tools

- T022: Create `JauntyDiagnosticListener` wrapper around DiagnosticSource
- T023: Define event names and payload builders (Executing, Executed, Failed)
- T024: Emit events from `InterceptorPipeline` at appropriate points
- T025: Write unit tests for `JauntyDiagnosticListener`
- T026: Write integration tests verifying event subscription receives events
- T027: Add documentation for OpenTelemetry/App Insights integration

**Checkpoint**: US3 complete + tested - DiagnosticSource events working

---

## Phase 6: Polish

- T028: Run full test suite (`dotnet test`)
- T029: Validate backward compatibility (existing tests pass without config)
- T030: Measure interceptor overhead (verify <5μs per no-op interceptor)
- T031: Code cleanup and style validation
- T032: Update main README with logging/interception features
- T033: Update feature-gap-analysis.md with completion status

---

## Execution Order

1. **Setup (Phase 1)** — Start immediately: T001, T002, T003 (parallel)
2. **Foundational (Phase 2)** — Blocks user stories: T004, T005, T006, T007
3. **User Story 1 (Phase 3)** — MVP Logging: T008–T013
4. **User Story 2 (Phase 4)** — MVP Interception: T014–T021
5. **User Story 3 (Phase 5)** — DiagnosticSource: T022–T027
6. **Polish (Phase 6)** — Final validation: T028–T033

**Parallel**: All `[P]` tasks can run concurrently

---

## Task Summary

| Phase | Tasks | MVP |
|-------|-------|-----|
| Setup | T001–T003 | No |
| Foundational | T004–T007 | No |
| US1: ILogger | T008–T013 | **Yes** |
| US2: Interception | T014–T021 | **Yes** |
| US3: DiagnosticSource | T022–T027 | No |
| Polish | T028–T033 | No |

**MVP Complete**: After Phase 4 (T021) — Logging and Interception production-ready

---

**Commit**: After each task | **Test**: `dotnet test`
