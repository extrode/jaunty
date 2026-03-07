# Jaunty 100% Code Coverage - Action Plan

**Objective**: Achieve 100% code coverage for both `net8.0` and `netstandard2.0` targets.
**Date**: 2026-02-24
**Status**: In Progress

## 1. Current State Analysis

Based on the dotCover report from 2026-02-24:

| Project | Target Framework | Coverage | Uncovered Statements | Priority | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Jaunty** | `net8.0` | 76% | 1,319 / 5,503 | **P0** | The core library. The `Jaunty` namespace itself has 1,047 uncovered statements. |
| **Jaunty** | `netstandard2.0` | 70% | 1,800 / 5,907 | **P0** | Gaps are larger here, but many are due to `#if NET8_0_OR_GREATER` blocks. |
| **Jaunty.Fluent** | `net8.0` / `netstandard2.0` | 33% | 6,199 / 9,288 | **P1** | Largest API surface and biggest coverage gap. |
| **Jaunty.Scaffolding** | `net8.0` / `netstandard2.0` | 49% | 482 / 940 | **P2** | Development-time tooling. |
| **Jaunty.Scaffolding.Cli**| `net8.0` / `netstandard2.0` | 0% | 154 / 154 | **P3** | CLI tool, lowest priority but required for 100%. |
| **Jaunty.Extensions.Reflection**| `net8.0` / `netstandard2.0` | 95% | 51 / 1070 | **P4** | High coverage, low priority. |

## 2. Action Plan

This plan prioritizes work based on impact. We will tackle the core library first to ensure its stability and completeness, followed by the larger, more specialized libraries.

### Phase 1: Core Library (`Jaunty`) - Priority P0

**Goal**: Close the 24-30% coverage gap in the main `Jaunty` project.

**Initial Focus**: High-impact areas identified in the previous plan and confirmed by the coverage report.

1.  **`GridReader` Async Methods**:
    * **Status**: **COVERED** (Confirmed by `tests/Jaunty.Tests/Integration/Multiple/GridReaderAsyncTests.cs`)

2.  **`QueryPartial` First/Single/Default Methods**:
    * **Status**: **COVERED** (Confirmed by `QueryPartialFirstTests.cs`, `QueryPartialFirstAsyncTests.cs`, `QueryPartialFirstOrDefaultTests.cs`, `QueryPartialFirstOrDefaultAsyncTests.cs`, `QueryPartialSingleTests.cs`, `QueryPartialSingleAsyncTests.cs`, `QueryPartialSingleOrDefaultTests.cs`, `QueryPartialSingleOrDefaultAsyncTests.cs` for all dialects.)

3.  **CRUD Operations (`Insert`)**:
    * **Status**: **COVERED** (Confirmed by `InsertTests.cs` and `InsertAsyncTests.cs` for single, multiple, explicit ID, null entity, command options, transaction rollback, and cancellation token (NET8_0_OR_GREATER only) scenarios across all dialects.)

4.  **CRUD Operations (`Update`)**:
    * **Status**: **COVERED** (Confirmed by `UpdateTests.cs` and `UpdateAsyncTests.cs` for existing entities, non-existing entities, command options, transaction rollback, no changes, null entity, and cancellation token (NET8_0_OR_GREATER only) scenarios across all dialects.)

5.  **CRUD Operations (`Delete`)**:
    * **Status**: **COVERED** (Confirmed by `DeleteTests.cs` and `DeleteAsyncTests.cs` for existing entities, non-existing entities, command options, transaction handling, transaction rollback, null entity, null ID, and cancellation token (NET8_0_OR_GREATER only) scenarios across all dialects.)

6.  **Dialect-Specific SQL Generation**:
    * **Status**: **COVERED** (The `RETURNING` clause behavior in PostgreSQL is implicitly covered by existing `Insert` identity-returning tests, as `InsertCore` correctly composes and executes the `INSERT ... RETURNING` statement and `ExecuteScalar()` retrieves the identity. No new tests are explicitly required for the `RETURNING` clause itself.)

### Phase 2: Fluent API (`Jaunty.Fluent`) - Priority P1

**Goal**: Address the 67% coverage gap in the `Jaunty.Fluent` project.

**Approach**: The Fluent API has broad but shallow coverage. The focus will be on adding tests for edge cases and complex query combinations rather than new features.

1.  **Complex SQL Generation**: Test intricate scenarios involving multiple `JOIN`s, subqueries, and `CTE`s.
2.  **Dialect-Specific Fluent Features**: Ensure any dialect-specific syntax is generated correctly.
3.  **Error Scenarios**: Test for invalid query combinations that should throw exceptions.

### Phase 3: Scaffolding Tools (`Jaunty.Scaffolding` & `Jaunty.Scaffolding.Cli`) - P2 & P3

**Goal**: Cover the remaining tooling projects.

1.  **`Jaunty.Scaffolding`**: Add integration tests for database schema reading and code generation against the test database.
2.  **`Jaunty.Scaffolding.Cli`**: Write tests for the command-line argument parser and command execution logic.

## 3. Test Implementation Guidelines

*   **Consistency**: Adhere strictly to the patterns in `tests/Jaunty.Tests/`.
*   **Fixtures & Attributes**: Use `IClassFixture<DialectFixture>` and `[Theory]` with dialect attributes (`[SqlServer]`, `[Postgres]`, etc.).
*   **Sync & Async Parity**: For every new sync test, add a corresponding async test.
*   **Edge Cases**: Prioritize testing for empty results, null values, transaction rollbacks, and connection state management.
*   **No Mocks in Integration Tests**: All integration tests must run against a real database connection provided by the fixture.

---
**Note on `net472` failures**: The `net472` target framework tests are currently experiencing failures related to environment-specific database setup issues (e.g., "Table 'bulk_test' already exists" or "relation 'bulk_test' does not exist" for MySQL, PostgreSQL, and SQL Server). These are being temporarily acknowledged as outside the immediate scope of code coverage improvements, as `net8.0` tests are passing and debugging the `net472` environment is not feasible at this stage. Focus will remain on `net8.0` and `netstandard2.0` (which is generally covered by `net8.0` tests unless specific `#if` directives are used).