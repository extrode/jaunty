# FlatFileSpec.md — Jaunty.FlatFiles Implementation Guide

> This document is the entry point for anyone implementing Jaunty.FlatFiles.
> Read this FIRST, then the the spec templates documents in order.

---

## Project Overview

You are implementing **Jaunty.FlatFiles** — an extension to the Jaunty micro-ORM that adds flat file (CSV, TSV, Parquet, JSON) query, CRUD, and import capabilities using DuckDB as the embedded engine.

## Repository Context

This project lives in the **existing Jaunty solution** as new projects added to `Jaunty.sln`. You are NOT creating a new repository.

### Before You Start

1. Familiarize yourself with Jaunty's existing codebase:
   - `IDialect` interface — you'll implement a DuckDB variant
   - Entity mapping system (`[Table]`, `[Column]`, `[Key]` attributes)
   - Materialization pipeline (how `DbDataReader` → C# entity works)
   - Fluent query API (how `.Where()`, `.OrderBy()`, `.Take()` generate SQL)
   - Method overloading style (NOT optional parameters)
2. Understand Jaunty's coding conventions:
   - Zero-allocation patterns on hot paths
   - No runtime reflection (cached/compiled)
   - `ValueTask` preferred over `Task`
   - Explicit > implicit

## Document Reading Order

```
1. docs/constitution.md     ← Governing principles (READ FIRST)
2. docs/specs/spec.md              ← What we're building and why
3. docs/specs/plan.md              ← How we're building it (architecture)
4. docs/specs/data-model.md        ← Interfaces, types, mappings
5. docs/specs/milestones.md        ← Feature list, priorities, gates
6. docs/specs/tasks.md             ← Task breakdown (execute these)
7. docs/architecture/*.mermaid         ← Visual architecture reference
```

## Execution Rules

### Test-Driven Development (MANDATORY)

For every task:
1. Write the test(s) FIRST
2. Run tests — they should FAIL (red)
3. Implement the minimum code to pass
4. Run tests — they should PASS (green)
5. Refactor if needed
6. Commit

### Commit Strategy

Every task = one commit. Format:

```
feat(flatfiles): T### — short description

- What was implemented
- What tests were added
- Key decisions made
```

### Branch Strategy

One branch per milestone:
- `feature/flatfiles-m0-foundation`
- `feature/flatfiles-m1-csv-tsv`
- `feature/flatfiles-m2-parquet-json`
- `feature/flatfiles-m3-crud-writeback`
- `feature/flatfiles-m4-import`
- `feature/flatfiles-m5-release`

### Milestone Gates

Do NOT proceed to the next milestone until:
1. All P0 tasks are complete and tests pass
2. All P1 tasks are complete or explicitly deferred with justification
3. `dotnet test` passes on ALL target frameworks
4. No compiler warnings

## Technical Constraints

### Package Structure (TWO separate packages)

| Package | Targets | Dependencies | NativeAOT |
|---|---|---|---|
| `Jaunty.FlatFiles` | netstandard2.0, net10.0 | Jaunty core only | Required |
| `Jaunty.FlatFiles.DuckDB` | net8.0, net10.0 | Jaunty.FlatFiles + DuckDB.NET.Data.Full | Best-effort |

### Key Architecture Decision: VIEW → TABLE Promotion

- Files open as DuckDB VIEWs (lazy, read-only)
- First INSERT/UPDATE/DELETE promotes to TABLE automatically
- This is transparent to the consumer — no explicit action needed
- SaveAsync uses COPY TO to write back to disk

### DuckDB.NET ADO.NET Provider

- Package: `DuckDB.NET.Data.Full` (includes native binaries for all platforms)
- Exposes: `DuckDBConnection`, `DuckDBCommand`, `DbDataReader`
- Quirks to watch for: parameter binding style, transaction behavior, NULL handling
- M0 is specifically designed to surface these quirks early

### API Style (Match Jaunty Core)

- Method overloading, NOT optional parameters
- Builder pattern for configuration
- `IDisposable` + `IAsyncDisposable` for lifecycle
- Extension methods for convenience (e.g., `FlatFileImporter.ImportAsync`)

## Test Infrastructure

- Framework: xUnit
- Assertions: FluentAssertions
- Benchmarks: BenchmarkDotNet
- Test data: committed fixtures in `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/`
- DuckDB instances: in-memory (`:memory:`), created per test
- NO MOCKS for DuckDB — use real instances

## When Stuck

- Check DuckDB SQL reference: https://duckdb.org/docs/sql/introduction
- Check DuckDB.NET repo: https://github.com/Giorgi/DuckDB.NET
- If DuckDB.NET ADO.NET behavior differs from expected, document it and create a thin adapter
- If a Jaunty core change is needed, document it as a separate PR and proceed with a workaround
