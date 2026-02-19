# Jaunty Inconsistencies - Prioritized Tasklist

**Generated:** 2026-02-19  
**Based on:** INCONSISTENCIES.md

---

## Priority Legend

- **P0 - Critical:** Must fix immediately; causes bugs or data corruption
- **P1 - High:** Should fix soon; affects reliability or API consistency
- **P2 - Medium:** Should fix; affects maintainability or developer experience
- **P3 - Low:** Nice to fix; minor inconsistencies or style issues

---

## P0 - Critical (8 issues)

### 1. Unsafe Transaction Type Casting in Async Methods
**Severity:** Critical  
**Files:** `Internals/Write/DeleteCore.cs`, `Internals/ExecuteQueryMultipleAsync.cs`  
**Issue:** Unsafe cast `(DbTransaction)options.Transaction` will throw if not DbTransaction  
**Fix:** Use pattern matching: `if (options.Transaction is DbTransaction dbTransaction)`  
**Estimated Effort:** 30 minutes  
**Risk:** Low - straightforward fix

**Tasks:**
- [ ] Fix DeleteCore.cs line ~170
- [ ] Fix DeleteCore.cs line ~210 (async version)
- [ ] Fix ExecuteQueryMultipleAsync.cs
- [ ] Add unit test for IDbTransaction (non-DbTransaction) implementation

---

### 2. Connection State Handling in Async Finally Blocks
**Severity:** Critical  
**Files:** Multiple async core methods  
**Issue:** Complex finally blocks with conditional compilation may not properly close connections  
**Fix:** Simplify connection close logic; ensure all code paths properly handle connection state  
**Estimated Effort:** 2 hours  
**Risk:** Medium - requires thorough testing

**Tasks:**
- [ ] Audit all async core methods for connection handling
- [ ] Standardize finally block pattern
- [ ] Add integration tests for connection state scenarios
- [ ] Test with various connection implementations

---

### 3. FK Constraint Toggle Error Handling
**Severity:** Critical  
**Files:** `Write/BulkInsert.cs`, `Write/BulkUpdate.cs`, `Write/BulkDelete.cs`  
**Issue:** Inconsistent error handling may leave database with FK constraints disabled  
**Fix:** Ensure all bulk operations re-enable FK constraints in catch/finally blocks  
**Estimated Effort:** 1 hour  
**Risk:** Medium - requires testing on all database types

**Tasks:**
- [ ] Audit all bulk operations for FK constraint handling
- [ ] Standardize error handling pattern
- [ ] Add tests for error scenarios during bulk operations
- [ ] Test on PostgreSQL, MySQL, SQLite (SQL Server doesn't support FK toggle)

---

### 4. Missing Null Checks in Public APIs
**Severity:** Critical  
**Files:** Some older public methods  
**Issue:** Not all public methods validate null parameters  
**Fix:** Add `ArgumentNullException.ThrowIfNull` checks to all public methods  
**Estimated Effort:** 1 hour  
**Risk:** Low - additive validation

**Tasks:**
- [ ] Audit all public methods for null checks
- [ ] Add missing null checks using NET8_0_OR_GREATER pattern
- [ ] Add unit tests for null parameter scenarios

---

### 5. Cancellation Token Not Checked in Bulk Loops
**Severity:** Critical  
**Files:** Bulk operation methods  
**Issue:** Large bulk operations don't respond to cancellation promptly  
**Fix:** Add `cancellationToken.ThrowIfCancellationRequested()` in loops  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Add cancellation checks to BulkInsertAsync
- [ ] Add cancellation checks to BulkUpdateAsync
- [ ] Add cancellation checks to BulkDeleteAsync
- [ ] Add tests for cancellation during bulk operations

---

### 6. SQL Server FK Toggle Returns Null
**Severity:** Critical  
**Files:** `Internals/Dialects/SqlServerDialect.cs`  
**Issue:** Returns `null` for FK toggle methods but callers may not check  
**Fix:** Ensure all callers check `SupportsForeignKeyToggle` before calling toggle methods  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Audit all FK toggle usage
- [ ] Add null checks or SupportsForeignKeyToggle checks
- [ ] Add tests for SQL Server bulk operations with ignoreConstraints

---

### 7. Composite Primary Key Support Gaps
**Severity:** Critical  
**Files:** Delete by ID methods  
**Issue:** `DeleteByIdCore` throws for composite keys but error message may not be clear  
**Fix:** Improve error messages and documentation  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Improve error message for composite key scenarios
- [ ] Add documentation about composite key limitations
- [ ] Add tests for composite key entities

---

### 8. Identity Column Detection Edge Cases
**Severity:** Critical  
**Files:** `Internals/Write/InsertCore.cs`  
**Issue:** Identity column detection may fail for non-standard naming  
**Fix:** Improve identity detection logic and error messages  
**Estimated Effort:** 1 hour  
**Risk:** Medium

**Tasks:**
- [ ] Review identity column detection logic
- [ ] Add support for [DatabaseGenerated] attribute
- [ ] Add tests for various identity column scenarios

---

## P1 - High (15 issues)

### 9. Standardize Null Check Pattern
**Severity:** High  
**Files:** Throughout codebase  
**Issue:** Three different null check patterns in use  
**Fix:** Standardize on `#if NET8_0_OR_GREATER` pattern  
**Estimated Effort:** 2 hours  
**Risk:** Low

**Tasks:**
- [ ] Create code snippet/template for null checks
- [ ] Update all core operation files
- [ ] Update all dialect files
- [ ] Update all helper classes

---

### 10. Standardize Exception Message Format
**Severity:** High  
**Files:** Throughout codebase  
**Issue:** Inconsistent exception message formats  
**Fix:** Create standard format: "Cannot {operation} {type}: {reason}"  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Define standard exception message format
- [ ] Update all InvalidOperationException messages
- [ ] Update all ArgumentException messages
- [ ] Update all NotSupportedException messages

---

### 11. Fix Return Type Inconsistency (Insert long vs int)
**Severity:** High  
**Files:** `Write/Insert.cs`, `Write/BulkInsert.cs`  
**Issue:** Insert returns `long`, BulkInsert returns `int`  
**Fix:** Decide on standard return type; document rationale  
**Estimated Effort:** 1 hour  
**Risk:** Medium - breaking change

**Tasks:**
- [ ] Discuss with team: keep `long` or standardize on `int`
- [ ] If changing, add obsoletion notice for old signature
- [ ] Update documentation
- [ ] Update tests

---

### 12. Add Generic Constraint `class` to All Methods
**Severity:** High  
**Files:** StoredProcedure methods  
**Issue:** Some methods missing `where T : class` constraint  
**Fix:** Add `class` constraint to all entity methods  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Audit all generic methods for constraints
- [ ] Add `class` constraint where missing
- [ ] Run tests to verify no regressions

---

### 13. Standardize Parameter Binding
**Severity:** High  
**Files:** `Write/InsertCore.cs`, `Write/UpdateCore.cs`, `Write/DeleteCore.cs`  
**Issue:** Delete uses inline binding, Insert/Update use compiled delegates  
**Fix:** Migrate Delete to use compiled delegates  
**Estimated Effort:** 2 hours  
**Risk:** Medium

**Tasks:**
- [ ] Create compiled delegate for Delete parameter binding
- [ ] Update DeleteCore to use WriteParameterCache
- [ ] Benchmark to verify performance improvement
- [ ] Update tests

---

### 14. Fix Dialect Method Implementation Gaps
**Severity:** High  
**Files:** All dialect files  
**Issue:** Not all dialects implement all interface methods consistently  
**Fix:** Ensure all dialects implement all methods, even if throwing NotSupportedException  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Audit ISqlDialect interface implementations
- [ ] Add missing method implementations
- [ ] Add tests for each dialect

---

### 15. Improve Identity Value Population
**Severity:** High  
**Files:** `Internals/Write/InsertCore.cs`  
**Issue:** Identity value only populated for IEntity<T> implementations  
**Fix:** Populate identity value for all entities with Id property  
**Estimated Effort:** 1 hour  
**Risk:** Medium

**Tasks:**
- [ ] Review identity population logic
- [ ] Add support for non-IEntity types
- [ ] Add tests for identity population

---

### 16. Standardize Connection Close Pattern
**Severity:** High  
**Files:** All core operation files  
**Issue:** Different patterns for closing connections in finally blocks  
**Fix:** Create single standard pattern for all methods  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Define standard connection close pattern
- [ ] Update all sync methods
- [ ] Update all async methods
- [ ] Test connection state scenarios

---

### 17. Add Missing Exception Documentation
**Severity:** High  
**Files:** Internal methods, older files  
**Issue:** Not all methods document exceptions  
**Fix:** Add `<exception>` tags to all methods that throw  
**Estimated Effort:** 2 hours  
**Risk:** Low

**Tasks:**
- [ ] Audit all methods for exception documentation
- [ ] Add missing `<exception>` tags
- [ ] Verify XML docs build without warnings

---

### 18. Fix SeeAlso References
**Severity:** High  
**Files:** Throughout codebase  
**Issue:** Inconsistent `<seealso>` usage  
**Fix:** Add comprehensive cross-references to all public methods  
**Estimated Effort:** 2 hours  
**Risk:** Low

**Tasks:**
- [ ] Define standard for `<seealso>` usage
- [ ] Add references to sync/async counterparts
- [ ] Add references to related operations
- [ ] Add references to overloads

---

### 19. Standardize Example Code Entity Names
**Severity:** High  
**Files:** Documentation examples  
**Issue:** Examples use different entity names  
**Fix:** Standardize on Product for simple examples  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Audit all code examples
- [ ] Standardize on Product/Customer/Order pattern
- [ ] Verify examples compile and work

---

### 20. Add Tests for Edge Cases
**Severity:** High  
**Files:** Test project  
**Issue:** Not all edge cases are tested  
**Fix:** Add comprehensive test coverage  
**Estimated Effort:** 4 hours  
**Risk:** Low

**Tasks:**
- [ ] Add tests for composite primary keys
- [ ] Add tests for null handling in all dialects
- [ ] Add tests for FK constraint toggle failures
- [ ] Add tests for transaction rollback scenarios
- [ ] Add tests for cancellation during operations

---

### 21. Fix Unsafe Async Transaction Handling
**Severity:** High  
**Files:** Multiple async core files  
**Issue:** Transaction disposal in async finally blocks  
**Fix:** Use proper async disposal pattern  
**Estimated Effort:** 1 hour  
**Risk:** Medium

**Tasks:**
- [ ] Audit all async transaction handling
- [ ] Use `await using` pattern where appropriate
- [ ] Test transaction disposal scenarios

---

### 22. Standardize Command Text Building
**Severity:** High  
**Files:** `Internals/Write/InsertCore.cs`, dialect files  
**Issue:** Different patterns for building SQL with identity columns  
**Fix:** Standardize on single pattern  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Review all SQL building patterns
- [ ] Standardize on StringBuilder or interpolation
- [ ] Add SQL injection prevention checks

---

### 23. Improve Error Messages for FK Toggle
**Severity:** High  
**Files:** Bulk operation files  
**Issue:** Error messages don't explain workaround  
**Fix:** Add helpful error messages with workarounds  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Improve error message for SQL Server FK toggle
- [ ] Add documentation about FK toggle limitations
- [ ] Add examples of alternative approaches

---

## P2 - Medium (16 issues)

### 24. Move QueryCore.cs to Internals/Read/
**Severity:** Medium  
**Files:** `Internals/QueryCore.cs`  
**Issue:** File organization inconsistency  
**Fix:** Move to `Internals/Read/` directory  
**Estimated Effort:** 30 minutes  
**Risk:** Low - requires namespace updates

**Tasks:**
- [ ] Create Internals/Read/ directory
- [ ] Move QueryCore.cs and QueryCoreAsync.cs
- [ ] Update namespaces and imports
- [ ] Verify build

---

### 25. Standardize Variable Names
**Severity:** Medium  
**Files:** Throughout codebase  
**Issue:** Inconsistent variable names for same concepts  
**Fix:** Create naming guide and update code  
**Estimated Effort:** 2 hours  
**Risk:** Low

**Tasks:**
- [ ] Create variable naming guide
- [ ] Update wasClosed/ownTransaction/entityList usage
- [ ] Use IDE refactoring tools

---

### 26. Unify Cache Implementations
**Severity:** Medium  
**Files:** Multiple cache files  
**Issue:** Different cache patterns in use  
**Fix:** Document rationale or unify implementations  
**Estimated Effort:** 4 hours  
**Risk:** Medium

**Tasks:**
- [ ] Audit all cache implementations
- [ ] Document rationale for each pattern
- [ ] Consider unifying if no clear rationale
- [ ] Benchmark performance

---

### 27. Add XML Comments to Internal Methods
**Severity:** Medium  
**Files:** Internal methods  
**Issue:** Internal methods lack documentation  
**Fix:** Add XML comments to core internal methods  
**Estimated Effort:** 3 hours  
**Risk:** Low

**Tasks:**
- [ ] Identify core internal methods
- [ ] Add summary comments
- [ ] Add param/returns documentation

---

### 28. Standardize Using Statement Style
**Severity:** Medium  
**Files:** Throughout codebase  
**Issue:** Mixed using declaration styles  
**Fix:** Standardize on modern `using var` pattern  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Update to `using var` pattern where appropriate
- [ ] Keep explicit dispose only where needed
- [ ] Verify no resource leaks

---

### 29. Fix AsyncEnumerable Conditional Compilation
**Severity:** Medium  
**Files:** `Readers/EntityReader.cs`  
**Issue:** Different return types based on compilation flag  
**Fix:** Consider abstraction or documentation  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Document the conditional compilation behavior
- [ ] Consider wrapper method for consistency
- [ ] Update API documentation

---

### 30. Improve Upsert Error Messages
**Severity:** Medium  
**Files:** `Write/Upsert.cs`  
**Issue:** Error messages don't explain database requirements  
**Fix:** Add database-specific error messages  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Improve error message for unsupported databases
- [ ] Add documentation about upsert requirements
- [ ] Add examples per database

---

### 31. Standardize Bulk Operation Return Values
**Severity:** Medium  
**Files:** Bulk operation files  
**Issue:** Return values may differ from single operations  
**Fix:** Document and standardize return value semantics  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Document what bulk operations return
- [ ] Ensure consistency across Insert/Update/Delete
- [ ] Update documentation

---

### 32. Add Performance Benchmarks
**Severity:** Medium  
**Files:** Test project  
**Issue:** No performance baseline  
**Fix:** Add benchmark tests  
**Estimated Effort:** 4 hours  
**Risk:** Low

**Tasks:**
- [ ] Set up BenchmarkDotNet
- [ ] Create benchmarks for common operations
- [ ] Document performance characteristics
- [ ] Add performance regression tests

---

### 33. Fix String Building in Dialects
**Severity:** Medium  
**Files:** Dialect files  
**Issue:** Some methods use string concatenation in loops  
**Fix:** Use StringBuilder for complex SQL generation  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Audit dialect SQL generation
- [ ] Convert to StringBuilder where appropriate
- [ ] Benchmark improvements

---

### 34. Standardize LINQ Usage
**Severity:** Medium  
**Files:** Throughout codebase  
**Issue:** Mixed LINQ vs manual iteration  
**Fix:** Create guidelines for LINQ usage  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Create LINQ usage guidelines
- [ ] Update code to follow guidelines
- [ ] Document performance considerations

---

### 35. Improve Parameter Name Validation
**Severity:** Medium  
**Files:** Parameter binding code  
**Issue:** Parameter name validation could be clearer  
**Fix:** Improve error messages for parameter mismatches  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Improve parameter validation error messages
- [ ] Add examples of correct parameter naming
- [ ] Add tests for parameter validation

---

### 36. Add Diagnostic Logging Hooks
**Severity:** Medium  
**Files:** Core operation files  
**Issue:** No built-in SQL logging  
**Fix:** Add optional SQL logging for debugging  
**Estimated Effort:** 3 hours  
**Risk:** Low

**Tasks:**
- [ ] Design SQL logging interface
- [ ] Add logging hooks to core operations
- [ ] Document logging configuration
- [ ] Add examples

---

### 37. Standardize Method Order in Files
**Severity:** Medium  
**Files:** All API files  
**Issue:** Methods not always in consistent order  
**Fix:** Create file organization standard  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Define method order standard
- [ ] Reorganize files to follow standard
- [ ] Update code generation templates if any

---

### 38. Add Code Analysis Rules
**Severity:** Medium  
**Files:** Project file  
**Issue:** No enforced coding standards  
**Fix:** Add .editorconfig or analyzer rules  
**Estimated Effort:** 2 hours  
**Risk:** Low

**Tasks:**
- [ ] Review existing .editorconfig
- [ ] Add Jaunty-specific rules
- [ ] Add analyzer packages if needed
- [ ] Fix existing violations

---

### 39. Improve Obsolete Attribute Usage
**Severity:** Medium  
**Files:** Legacy API methods  
**Issue:** Obsolete methods may not have clear migration path  
**Fix:** Add detailed migration guidance to Obsolete attributes  
**Estimated Effort:** 30 minutes  
**Risk:** Low

**Tasks:**
- [ ] Audit all [Obsolete] attributes
- [ ] Add migration guidance to messages
- [ ] Add documentation about deprecated APIs

---

## P3 - Low (8 issues)

### 40. Fix Class Naming Inconsistencies
**Severity:** Low  
**Files:** Internal classes  
**Issue:** Mixed naming patterns  
**Fix:** Rename for consistency  
**Estimated Effort:** 1 hour  
**Risk:** Low

**Tasks:**
- [ ] Identify naming inconsistencies
- [ ] Create naming standard
- [ ] Rename classes using refactoring tools

---

### 41. Standardize Method Naming for Core Methods
**Severity:** Low  
**Files:** Internal core methods  
**Issue:** Minor naming variations  
**Fix:** Standardize naming pattern  
**Estimated Effort:** 30 minutes  
**Risk:** Low

---

### 42. Fix Comment Styles
**Severity:** Low  
**Files:** Throughout codebase  
**Issue:** Mixed comment styles  
**Fix:** Standardize on XML comments  
**Estimated Effort:** 1 hour  
**Risk:** Low

---

### 43. Improve Dialect Documentation
**Severity:** Low  
**Files:** Dialect files  
**Issue:** Dialect classes could use more documentation  
**Fix:** Add class-level XML comments  
**Estimated Effort:** 30 minutes  
**Risk:** Low

---

### 44. Add Architecture Decision Records
**Severity:** Low  
**Files:** New docs/  
**Issue:** Design decisions not documented  
**Fix:** Create ADRs for key decisions  
**Estimated Effort:** 4 hours  
**Risk:** Low

---

### 45. Create Contributing Guide
**Severity:** Low  
**Files:** New docs/  
**Issue:** No contribution guidelines  
**Fix:** Create CONTRIBUTING.md  
**Estimated Effort:** 2 hours  
**Risk:** Low

---

### 46. Add API Design Guidelines
**Severity:** Low  
**Files:** New docs/  
**Issue:** API design decisions not documented  
**Fix:** Create API-DESIGN.md  
**Estimated Effort:** 2 hours  
**Risk:** Low

---

### 47. Create Code Review Checklist
**Severity:** Low  
**Files:** New docs/  
**Issue:** No code review standard  
**Fix:** Create CODE-REVIEW.md checklist  
**Estimated Effort:** 1 hour  
**Risk:** Low

---

## Summary by Priority

| Priority | Count | Estimated Hours |
|----------|-------|-----------------|
| P0 - Critical | 8 | 9.5 |
| P1 - High | 15 | 24.5 |
| P2 - Medium | 16 | 32 |
| P3 - Low | 8 | 16 |
| **Total** | **47** | **82** |

---

## Recommended Phases

### Phase 1 (Week 1-2): Critical Issues
- Complete all P0 items
- Focus on connection handling and transaction safety
- Add missing null checks and cancellation support

### Phase 2 (Week 3-4): High Priority
- Complete P1 items 9-15
- Standardize null checks and exception messages
- Fix return type inconsistencies

### Phase 3 (Week 5-6): High Priority Continued
- Complete P1 items 16-23
- Improve documentation and tests
- Fix async transaction handling

### Phase 4 (Week 7-8): Medium Priority
- Complete P2 items 24-30
- Reorganize code structure
- Add internal documentation

### Phase 5 (Week 9-10): Medium Priority Continued
- Complete P2 items 31-39
- Add benchmarks and diagnostics
- Improve code quality tooling

### Phase 6 (Week 11-12): Low Priority
- Complete P3 items 40-47
- Naming and style cleanup
- Create documentation and guides

---

*End of Tasklist*
