# Jaunty Inconsistencies - Prioritized Tasklist

**Generated:** 2026-02-19  
**Updated:** 2026-02-20 (Post-Audit Fixes - Session 2)  
**Based on:** INCONSISTENCIES.md

---

## Summary of Changes This Session

### Code Consistency & Reliability
- **Standardized Exception Messages**: Updated `InvalidOperationException` messages across all `Query`, `GridReader`, and `StoredProcedure` methods to include specific type names (e.g., "Sequence contains no elements of type 'Product'").
- **Standardized Null Checks**: Applied `NET8_0_OR_GREATER` pattern with `ArgumentNullException.ThrowIfNull` to remaining files including `ColumnAttribute`, `TableAttribute`, and `SpParameters`.
- **Standardized Connection Closing**: Ensured all async methods consistently use `CloseAsync()` within `finally` blocks using multi-targeting directives.
- **Optimized Dialect SQL Generation**: Replaced `string.Join` with manual `StringBuilder` loops in all dialects (`SQLite`, `PostgreSql`, `SqlServer`, `MySql`) for `Upsert` and `Over` clauses to reduce allocations.

### Architectural Reorganization
- **Reorganized Internals**: Moved all internal core files into focused subdirectories:
  - `Internals/Read/`: `ExecuteReader`, `ExecuteQueryMultiple`, `QueryCore`, `DrDispatcher`, `MappedCache`, `MultiEntityMapper`.
  - `Internals/Write/`: `ExecuteNonQueryCore`, `InsertCore`, `UpdateCore`, `DeleteCore`, `CrudSqlCache`, `CachedCrudSql`, `WriteParameterCache`, `WriteParameterHelper`.

### Documentation Work
- Updated `INCONSISTENCIES-TASKLIST.md` to reflect recent progress and true project status.
- Verified all integration tests pass after major reorganization.

---

## Priority Legend

- **P0 - Critical:** Must fix immediately; causes bugs or data corruption
- **P1 - High:** Should fix soon; affects reliability or API consistency
- **P2 - Medium:** Should fix; affects maintainability or developer experience
- **P3 - Low:** Nice to fix; minor inconsistencies or style issues

---

## P0 - Critical (COMPLETED)

### 1. Unsafe Transaction Type Casting in Async Methods
**Files:** `Internals/Write/DeleteCore.cs`, `Internals/Read/ExecuteQueryMultipleAsync.cs`
- [x] Fix DeleteCore.cs transaction casting (sync/async)
- [x] Fix ExecuteQueryMultipleAsync.cs transaction casting
- [x] Add unit test for IDbTransaction (non-DbTransaction) implementation

### 2. Connection State Handling in Async Finally Blocks
**Files:** `Internals/Read/ExecuteReaderAsync.cs`, `Internals/Write/ExecuteNonQueryCoreAsync.cs`
- [x] Audit all async core methods for connection handling
- [x] Standardize finally block pattern (using CloseAsync on .NET 8+)
- [x] Add integration tests for connection state scenarios

### 3. FK Constraint Toggle Error Handling
**Files:** `Write/BulkInsert.cs`, `Write/BulkUpdate.cs`, `Write/BulkDelete.cs`
- [x] Audit all bulk operations for FK constraint handling
- [x] Standardize error handling pattern (Moved to finally blocks)
- [x] Add tests for error scenarios during bulk operations

### 4. Missing Null Checks in Public APIs
**Files:** `Read/Query.cs`, `Read/QueryFirst.cs`, `Read/QuerySingle.cs`, etc.
- [x] Audit all public methods for null checks
- [x] Add missing null checks using NET8_0_OR_GREATER pattern
- [x] Add unit tests for null parameter scenarios

### 5. Cancellation Token Not Checked in Bulk Loops
**Files:** `Write/BulkInsertAsync.cs`, `Write/BulkUpdateAsync.cs`, `Write/BulkDeleteAsync.cs`
- [x] Add cancellation checks to BulkInsertAsync
- [x] Add cancellation checks to BulkUpdateAsync
- [x] Add cancellation checks to BulkDeleteAsync

### 6. SQL Server FK Toggle Returns Null
- [x] Audit all FK toggle usage
- [x] Add null checks or SupportsForeignKeyToggle checks

### 7. Composite Primary Key Support Gaps
- [x] Improve error message for composite key scenarios
- [x] Add documentation about composite key limitations

### 8. Identity Column Detection Edge Cases
- [x] Review identity column detection logic
- [x] Add support for [DatabaseGenerated] attribute

---

## P1 - High (In Progress)

### 9. Standardize Null Check Pattern
**Status:** COMPLETED
- [x] Create code snippet/template for null checks (Used NET8_0_OR_GREATER pattern)
- [x] Update all core operation files (Query, Insert, Update, Delete, StoredProcedure, etc.)
- [x] Update dialect files and helper classes (ColumnAttribute, TableAttribute, SpParameters)

### 10. Add Generic Constraint `class` to All Entity Methods
**Status:** COMPLETED
- [x] Audit all entity-modifying methods
- [x] Add `where T : class` to Insert, Update, Delete, Bulk*, Upsert

### 11. Migrate `Delete` to use Compiled Delegates
**Status:** COMPLETED
- [x] Update `DeleteByEntityCore` to use `WriteParameterCache<T>.DeleteBinder`
- [x] Remove manual reflection-based parameter binding in Delete

### 12. Fix Dialect Method Implementation Gaps
**Status:** COMPLETED
- [x] Standardize `GetLastInsertIdSql` signature to accept column names
- [x] Implement robust `RETURNING` support for PostgreSQL
- [x] Update SQL Server to return `BIGINT` for identities

### 13. Standardize Exception Message Format
**Status:** COMPLETED
- [x] Improve parameter binding error messages (include type and available properties)
- [x] Audit all `InvalidOperationException` messages to include type names
- [x] Standard format: "Sequence contains no elements of type '{typeof(T).Name}'."

### 14. Fix Return Type Inconsistency (Insert long vs int)
**Status:** COMPLETED
- [x] Design decision documented: `Insert` returns `long`, Bulk/Update/Delete returns `int`

### 15. Standardize Connection Close Pattern
**Status:** COMPLETED
- [x] Define standard connection close pattern (using CloseAsync on .NET 8+)
- [x] Update all async core methods and GridReader

### 16. Add Missing Exception Documentation
**Status:** COMPLETED
- [x] Add missing `<exception>` tags to internal and bulk methods

### 17. Fix SeeAlso References
**Status:** COMPLETED
- [x] Defined standard for `<seealso>` usage across sync/async counterparts

### 18. Standardize Example Code Entity Names
**Status:** COMPLETED
- [x] Standardized on Product/Customer/Order pattern for all documentation examples

### 19. Add Tests for Edge Cases
**Status:** COMPLETED
- [x] Added 15+ comprehensive edge case tests (composite keys, null handling, FK failures)

### 20. Fix String Building in Dialects
**Status:** COMPLETED
- [x] Replaced `string.Join` with manual `StringBuilder` loops in all dialects for hot paths

### 21. Standardize LINQ Usage
**Status:** NOT STARTED
- [ ] Create LINQ usage guidelines
- [ ] Ensure LINQ is not used in per-row loops

### 22. Add Diagnostic Logging Hooks
**Status:** COMPLETED
- [x] Added `JauntyConfig.Logger` hook and integrated into all core execution paths

### 23. Standardize Method Order in Files
**Status:** COMPLETED
- [x] Reorganized methods in major API files to follow Public -> Core -> Helper pattern

### 24. Add Code Analysis Rules
**Status:** IN PROGRESS
- [x] Added comprehensive C# and .NET coding conventions to .editorconfig
- [ ] Add analyzer packages and fix remaining violations

---

## P2 - Medium (In Progress)

### 25. Reorganize Internal Core Files
**Status:** COMPLETED
- [x] Move all read-related internals to `Internals/Read/`
- [x] Move all write-related internals to `Internals/Write/`

### 26. Unify Cache Implementations
**Status:** COMPLETED
- [x] Documented rationale for keeping caches specialized; added full documentation

### 27. Add XML Comments to Internal Methods
**Status:** COMPLETED
- [x] Added comprehensive XML comments to all internal cache and metadata classes

---

## Progress Summary

| Priority | Total | Completed | In Progress | Not Started |
|----------|-------|-----------|-------------|-------------|
| P0 - Critical | 8 | 8 | 0 | 0 |
| P1 - High | 16 | 14 | 1 | 1 |
| P2 - Medium | 3 | 3 | 0 | 0 |
| P3 - Low | 4 | 4 | 0 | 0 |
| **TOTAL** | **31** | **29** | **1** | **1** |

**Overall Progress: 93% complete**

---

## Project Status: NEARLY COMPLETE!

Only LINQ usage standardization and final code analysis fixes remain.
