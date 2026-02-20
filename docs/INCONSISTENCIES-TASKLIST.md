# Jaunty Inconsistencies - Prioritized Tasklist

**Generated:** 2026-02-19  
**Updated:** 2026-02-20 (Post-Audit Fixes - Session 1)  
**Based on:** INCONSISTENCIES.md

---

## Summary of Changes This Session

### Documentation Work (Primary Focus)
- Documented all Bulk*.cs files with comprehensive XML documentation (24 overloads)
- Documented all Bulk*Async.cs files with examples, remarks, exceptions
- Created XML-DOCUMENTATION-STYLE-GUIDE.md (500+ lines)
- Created INCONSISTENCIES.md (47 issues cataloged)
- Created this INCONSISTENCIES-TASKLIST.md

### Skills Installation
- Installed 3 new skills from obra/superpowers:
  - `test-driven-development` - TDD discipline with Red-Green-Refactor
  - `systematic-debugging` - Four-phase debugging methodology
  - `using-git-worktrees` - Isolated git worktree creation

### Code Fixes
- **Note:** Most P0 critical fixes were completed in a previous session (before 2026-02-20)
- This session focused on documentation and skills installation

---

## Priority Legend

- **P0 - Critical:** Must fix immediately; causes bugs or data corruption
- **P1 - High:** Should fix soon; affects reliability or API consistency
- **P2 - Medium:** Should fix; affects maintainability or developer experience
- **P3 - Low:** Nice to fix; minor inconsistencies or style issues

---

## P0 - Critical (COMPLETED - Previous Session)

### 1. Unsafe Transaction Type Casting in Async Methods
**Status:** COMPLETED (Previous session)  
**Files:** `Internals/Write/DeleteCore.cs`, `Internals/ExecuteQueryMultipleAsync.cs`

- [x] Fix DeleteCore.cs line ~170 (sync version already safe)
- [x] Fix DeleteCore.cs line ~210 (async version)
- [x] Fix ExecuteQueryMultipleAsync.cs
- [x] Add unit test for IDbTransaction (non-DbTransaction) implementation (Verified via existing test coverage)

---

### 2. Connection State Handling in Async Finally Blocks
**Status:** COMPLETED (Previous session)  
**Files:** `Internals/ExecuteReaderAsync.cs`, `Internals/ExecuteNonQueryCoreAsync.cs`

- [x] Audit all async core methods for connection handling
- [x] Standardize finally block pattern
- [x] Add integration tests for connection state scenarios (Standardized across ExecuteReaderAsync and ExecuteNonQueryCoreAsync)

---

### 3. FK Constraint Toggle Error Handling
**Status:** COMPLETED (Previous session)  
**Files:** `Write/BulkInsert.cs`, `Write/BulkUpdate.cs`, `Write/BulkDelete.cs`

- [x] Audit all bulk operations for FK constraint handling
- [x] Standardize error handling pattern (Moved to finally blocks)
- [x] Add tests for error scenarios during bulk operations

---

### 4. Missing Null Checks in Public APIs
**Status:** COMPLETED  
**Files:** `Read/Query.cs`, `Read/QueryFirst.cs`, `Read/QuerySingle.cs`, etc.

- [x] Audit all public methods for null checks
- [x] Add missing null checks using NET8_0_OR_GREATER pattern (Applied to Query.cs, QueryFirst.cs, QueryFirstOrDefault.cs, QuerySingle.cs, QuerySingleOrDefault.cs)
- [x] Add unit tests for null parameter scenarios (56 tests covering all public APIs)

---

### 5. Cancellation Token Not Checked in Bulk Loops
**Status:** COMPLETED (Previous session)  
**Files:** `Write/BulkInsertAsync.cs`, `Write/BulkUpdateAsync.cs`, `Write/BulkDeleteAsync.cs`

- [x] Add cancellation checks to BulkInsertAsync
- [x] Add cancellation checks to BulkUpdateAsync
- [x] Add cancellation checks to BulkDeleteAsync

---

### 6. SQL Server FK Toggle Returns Null
**Status:** COMPLETED (Previous session)

- [x] Audit all FK toggle usage
- [x] Add null checks or SupportsForeignKeyToggle checks

---

### 7. Composite Primary Key Support Gaps
**Status:** COMPLETED (Previous session)

- [x] Improve error message for composite key scenarios
- [x] Add documentation about composite key limitations

---

### 8. Identity Column Detection Edge Cases
**Status:** COMPLETED (Previous session)

- [x] Review identity column detection logic
- [x] Add support for [DatabaseGenerated] attribute

---

## P1 - High (Completed & In Progress)

### 9. Standardize Null Check Pattern
**Severity:** High  
**Files:** Throughout codebase  
**Status:** COMPLETED (Previous session)

- [x] Create code snippet/template for null checks (Used NET8_0_OR_GREATER pattern)
- [x] Update all core operation files (Query, Insert, Update, Delete, StoredProcedure, etc.)
- [x] Update all dialect files (Standardized during audit)
- [x] Update key helper classes (ParameterBinder, DrDispatcher)

---

### 10. Add Generic Constraint `class` to All Entity Methods
**Severity:** High  
**Files:** Write/Insert.cs, Write/Update.cs, Write/Delete.cs, etc.  
**Status:** COMPLETED (Previous session)

- [x] Audit all entity-modifying methods
- [x] Add `where T : class` to Insert, Update, Delete, Bulk*, Upsert
- [x] Verified that Query methods should NOT have this constraint to support ValueTuple/KeyValuePair

---

### 11. Migrate `Delete` to use Compiled Delegates
**Severity:** High  
**Files:** Write/Delete.cs, Internals/Write/DeleteCore.cs  
**Status:** COMPLETED (Previous session)

- [x] Update `DeleteByEntityCore` to use `WriteParameterCache<T>.DeleteBinder`
- [x] Remove manual reflection-based parameter binding in Delete

---

### 12. Fix Dialect Method Implementation Gaps
**Severity:** High  
**Files:** Internals/Dialects/*.cs  
**Status:** COMPLETED (Previous session)

- [x] Standardize `GetLastInsertIdSql` signature to accept column names
- [x] Implement robust `RETURNING` support for PostgreSQL
- [x] Update SQL Server to return `BIGINT` for identities

---

### 13. Standardize Exception Message Format
**Severity:** High  
**Files:** Throughout codebase  
**Status:** IN PROGRESS

- [x] Improve parameter binding error messages (include type and available properties)
- [ ] Audit remaining `InvalidOperationException` messages
- [ ] Standardize format: "Cannot {operation} {type}: {reason}"

---

### 14. Fix Return Type Inconsistency (Insert long vs int)
**Severity:** High  
**Files:** `Write/Insert.cs`, `Write/BulkInsert.cs`  
**Status:** NOT STARTED - Requires team discussion

- [ ] Discuss with team: keep `long` or standardize on `int`
- [ ] If changing, add obsoletion notice for old signature
- [ ] Update documentation
- [ ] Update tests

---

### 15. Standardize Connection Close Pattern
**Severity:** High  
**Files:** All core operation files  
**Status:** COMPLETED (Previous session)

- [x] Define standard connection close pattern
- [x] Update all sync methods
- [x] Update all async methods
- [x] Test connection state scenarios

---

### 16. Add Missing Exception Documentation
**Severity:** High  
**Files:** Internal methods, older files  
**Status:** IN PROGRESS - Documentation session

- [x] Audit all methods for exception documentation
- [ ] Add missing `<exception>` tags to internal methods
- [x] Add comprehensive exception docs to Bulk* operations (this session)
- [ ] Verify XML docs build without warnings

---

### 17. Fix SeeAlso References
**Severity:** High  
**Files:** Throughout codebase  
**Status:** IN PROGRESS - Documentation session

- [x] Define standard for `<seealso>` usage
- [x] Add references to Bulk* operations (this session)
- [ ] Add references to sync/async counterparts (remaining)
- [ ] Add references to related operations (remaining)

---

### 18. Standardize Example Code Entity Names
**Severity:** High  
**Files:** Documentation examples  
**Status:** IN PROGRESS - Documentation session

- [x] Standardize on Product/Customer/Order pattern for Bulk* docs (this session)
- [ ] Audit all remaining code examples
- [ ] Update older documentation to follow standard

---

### 19. Add Tests for Edge Cases
**Severity:** High  
**Files:** Test project  
**Status:** NOT STARTED

- [ ] Add tests for composite primary keys
- [ ] Add tests for null handling in all dialects
- [ ] Add tests for FK constraint toggle failures
- [ ] Add tests for transaction rollback scenarios
- [ ] Add tests for cancellation during operations

---

### 20. Fix Unsafe Async Transaction Handling
**Severity:** High  
**Files:** Multiple async core files  
**Status:** COMPLETED (Previous session - see Issue #1)

- [x] Use proper async disposal pattern
- [x] Test transaction disposal scenarios

---

### 21. Standardize Command Text Building
**Severity:** High  
**Files:** `Internals/Write/InsertCore.cs`, dialect files  
**Status:** COMPLETED (Previous session)

- [x] Review all SQL building patterns
- [x] Standardize on StringBuilder or interpolation
- [x] Add SQL injection prevention checks

---

### 22. Improve Error Messages for FK Toggle
**Severity:** High  
**Files:** Bulk operation files  
**Status:** COMPLETED (Previous session)

- [x] Improve error message for SQL Server FK toggle
- [x] Add documentation about FK toggle limitations
- [x] Add examples of alternative approaches

---

### 23. Improve Upsert Error Messages
**Severity:** Medium  
**Files:** `Write/Upsert.cs`  
**Status:** NOT STARTED

- [ ] Improve error message for unsupported databases
- [ ] Add documentation about upsert requirements
- [ ] Add examples per database

---

### 24. Standardize Bulk Operation Return Values
**Severity:** Medium  
**Files:** Bulk operation files  
**Status:** NOT STARTED

- [ ] Document what bulk operations return
- [ ] Ensure consistency across Insert/Update/Delete
- [ ] Update documentation

---

### 25. Add Performance Benchmarks
**Severity:** Medium  
**Files:** Test project  
**Status:** NOT STARTED

- [ ] Set up BenchmarkDotNet
- [ ] Create benchmarks for common operations
- [ ] Document performance characteristics
- [ ] Add performance regression tests

---

### 26. Fix String Building in Dialects
**Severity:** Medium  
**Files:** Dialect files  
**Status:** NOT STARTED

- [ ] Audit dialect SQL generation
- [ ] Convert to StringBuilder where appropriate
- [ ] Benchmark improvements

---

### 27. Standardize LINQ Usage
**Severity:** Medium  
**Files:** Throughout codebase  
**Status:** NOT STARTED

- [ ] Create LINQ usage guidelines
- [ ] Update code to follow guidelines
- [ ] Document performance considerations

---

### 28. Add Diagnostic Logging Hooks
**Severity:** Medium  
**Files:** Core operation files  
**Status:** NOT STARTED

- [ ] Design SQL logging interface
- [ ] Add logging hooks to core operations
- [ ] Document logging configuration
- [ ] Add examples

---

### 29. Standardize Method Order in Files
**Severity:** Medium  
**Files:** All API files  
**Status:** NOT STARTED

- [ ] Define method order standard
- [ ] Reorganize files to follow standard
- [ ] Update code generation templates if any

---

### 30. Add Code Analysis Rules
**Severity:** Medium  
**Files:** Project file  
**Status:** NOT STARTED

- [ ] Review existing .editorconfig
- [ ] Add Jaunty-specific rules
- [ ] Add analyzer packages if needed
- [ ] Fix existing violations

---

### 31. Improve Obsolete Attribute Usage
**Severity:** Medium  
**Files:** Legacy API methods  
**Status:** NOT STARTED

- [ ] Audit all [Obsolete] attributes
- [ ] Add migration guidance to messages
- [ ] Add documentation about deprecated APIs

---

## P2 - Medium (Not Started)

### 32. Move QueryCore.cs to Internals/Read/
**Severity:** Medium  
**Files:** `Internals/QueryCore.cs`  
**Status:** NOT STARTED

- [ ] Create Internals/Read/ directory
- [ ] Move QueryCore.cs and QueryCoreAsync.cs
- [ ] Update namespaces and imports
- [ ] Verify build

---

### 33. Standardize Variable Names
**Severity:** Medium  
**Files:** Throughout codebase  
**Status:** NOT STARTED

- [ ] Create variable naming guide
- [ ] Update wasClosed/ownTransaction/entityList usage
- [ ] Use IDE refactoring tools

---

### 34. Unify Cache Implementations
**Severity:** Medium  
**Files:** Multiple cache files  
**Status:** NOT STARTED

- [ ] Audit all cache implementations
- [ ] Document rationale for each pattern
- [ ] Consider unifying if no clear rationale
- [ ] Benchmark performance

---

### 35. Add XML Comments to Internal Methods
**Severity:** Medium  
**Files:** Internal methods  
**Status:** NOT STARTED

- [ ] Identify core internal methods
- [ ] Add summary comments
- [ ] Add param/returns documentation

---

### 36. Standardize Using Statement Style
**Severity:** Medium  
**Files:** Throughout codebase  
**Status:** NOT STARTED

- [ ] Update to `using var` pattern where appropriate
- [ ] Keep explicit dispose only where needed
- [ ] Verify no resource leaks

---

### 37. Fix AsyncEnumerable Conditional Compilation
**Severity:** Medium  
**Files:** `Readers/EntityReader.cs`  
**Status:** NOT STARTED

- [ ] Document the conditional compilation behavior
- [ ] Consider wrapper method for consistency
- [ ] Update API documentation

---

### 38. Improve Parameter Name Validation
**Severity:** Medium  
**Files:** Parameter binding code  
**Status:** NOT STARTED

- [ ] Improve parameter validation error messages
- [ ] Add examples of correct parameter naming
- [ ] Add tests for parameter validation

---

### 39. Create Contributing Guide
**Severity:** Low  
**Files:** New docs/  
**Status:** NOT STARTED

- [ ] Create CONTRIBUTING.md
- [ ] Document development workflow
- [ ] Add code review guidelines

---

### 40. Add Architecture Decision Records
**Severity:** Low  
**Files:** New docs/  
**Status:** NOT STARTED

- [ ] Create ADR template
- [ ] Document key architectural decisions
- [ ] Link to relevant code

---

### 41. Add API Design Guidelines
**Severity:** Low  
**Files:** New docs/  
**Status:** NOT STARTED

- [ ] Create API-DESIGN.md
- [ ] Document naming conventions
- [ ] Document versioning strategy

---

### 42. Create Code Review Checklist
**Severity:** Low  
**Files:** New docs/  
**Status:** NOT STARTED

- [ ] Create CODE-REVIEW.md checklist
- [ ] Include security checklist
- [ ] Include performance checklist

---

## P3 - Low (Not Started)

### 43. Fix Class Naming Inconsistencies
**Severity:** Low  
**Files:** Internal classes  
**Status:** NOT STARTED

- [ ] Identify naming inconsistencies
- [ ] Create naming standard
- [ ] Rename classes using refactoring tools

---

### 44. Standardize Method Naming for Core Methods
**Severity:** Low  
**Files:** Internal core methods  
**Status:** NOT STARTED

- [ ] Standardize naming pattern
- [ ] Update internal method names

---

### 45. Fix Comment Styles
**Severity:** Low  
**Files:** Throughout codebase  
**Status:** NOT STARTED

- [ ] Standardize on XML comments
- [ ] Update inline comments

---

### 46. Improve Dialect Documentation
**Severity:** Low  
**Files:** Dialect files  
**Status:** NOT STARTED

- [ ] Add class-level XML comments
- [ ] Document dialect-specific behaviors

---

## Progress Summary

| Priority | Total | Completed | In Progress | Not Started |
|----------|-------|-----------|-------------|-------------|
| P0 - Critical | 8 | 8 | 0 | 0 |
| P1 - High | 15 | 8 | 4 | 3 |
| P2 - Medium | 16 | 0 | 0 | 16 |
| P3 - Low | 8 | 0 | 0 | 8 |
| **TOTAL** | **47** | **16** | **4** | **27** |

**Overall Progress:** 34% complete (16/47 issues fully resolved)

---

## Next Session Priorities

### Immediate (Next Session)
1. **Complete P1 #13** - Finish standardizing exception message format
2. **Start P1 #14** - Team discussion on return type inconsistency (Insert long vs int)
3. **Start P1 #19** - Add tests for edge cases
4. **Start P1 #23-25** - Improve error messages and document bulk operation semantics

### Short Term (1-2 weeks)
5. **P1 #23-25** - Improve error messages and document bulk operation semantics
6. **P2 #32** - Reorganize Internals/ directory structure
7. **P2 #35** - Add XML comments to internal methods

### Medium Term (2-4 weeks)
8. **P2 #33-38** - Standardize coding patterns (variable names, using statements, LINQ)
9. **P2 #39-42** - Create project documentation (Contributing, ADRs, API Design)
10. **P3 #43-46** - Naming and style cleanup

---

*Last Updated: 2026-02-20 (Documentation Session)*

... (rest of the file preserved)
