# Jaunty.FlatFiles Consistency Audit Tasklist

**Generated:** 2026-03-07  
**Scope:** Jaunty.FlatFiles and Jaunty.FlatFiles.DuckDB vs Jaunty core  
**Goal:** Ensure consistency in code quality, organization, performance, usage, and documentation

---

## Executive Summary

| Priority | Count | Description |
|----------|-------|-------------|
| **P0** | 7 | Critical issues (blocking) |
| **P1** | 12 | High priority (should fix) |
| **P2** | 11 | Medium priority (recommended) |
| **P3** | 7 | Low priority (nice to have) |
| **Total** | **37** | |

---

## P0: Critical Issues (Blocking)

### Documentation & XML Comments

- [ ] **P0-1: Add XML documentation to all public APIs in Jaunty.FlatFiles**
  - [ ] `IFileSource` interface members lack `<returns>` and `<exception>` tags
  - [ ] `FlatFileOptions` methods lack `<example>` tags
  - [ ] `IFlatFileDialect` interface members need complete documentation
  - [ ] All file source classes (`CsvFileSource`, `TsvFileSource`, etc.) need complete XML docs
  - **Files:** `src/Jaunty.FlatFiles/IFileSource.cs`, `src/Jaunty.FlatFiles/FlatFileOptions.cs`, `src/Jaunty.FlatFiles/IFlatFileDialect.cs`

- [ ] **P0-2: Add XML documentation to Jaunty.FlatFiles.DuckDB public APIs**
  - [ ] `DuckDb` class public methods lack complete documentation
  - [ ] `FlatFile` static factory methods need `<exception>` tags
  - [ ] `DuckDbDialect` needs documentation for all overridden methods
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`, `src/Jaunty.FlatFiles.DuckDB/FlatFile.cs`

- [ ] **P0-3: Add XML documentation to internal helper types**
  - [ ] `FlatFileExpressionHelper` class and members need documentation
  - [ ] `ColumnMapping` struct needs documentation
  - [ ] ImportPipeline classes need documentation
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/FlatFileExpressionHelper.cs`, `src/Jaunty.FlatFiles.DuckDB/ImportPipeline/`

### Code Style & Conventions

- [ ] **P0-4: Remove LINQ usage in hot paths (violates API-DESIGN.md)**
  - [ ] `DuckDb.QueryAsync<T>()` uses LINQ in materialization loop
  - [ ] `FlatFileExpressionHelper.GetColumnMappings()` uses LINQ
  - [ ] Replace with structured `for` loops on concrete collections
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`, `src/Jaunty.FlatFiles.DuckDB/FlatFileExpressionHelper.cs`
  - **Reference:** `docs/API-DESIGN.md` section "LINQ Usage Policy"

- [ ] **P0-5: Fix inconsistent null handling**
  - [ ] Jaunty core uses `#if NET8_0_OR_GREATER` conditional compilation for `ArgumentNullException.ThrowIfNull`
  - [ ] FlatFiles projects should match this pattern consistently
  - [ ] Some FlatFiles code uses direct null checks instead
  - **Files:** All FlatFiles source files

- [ ] **P0-6: Add `ConfigureAwait(false)` to all async library code**
  - [ ] `DuckDb.RegisterSourceAsync()` missing ConfigureAwait
  - [ ] `DuckDb.InsertAsync()` methods missing ConfigureAwait
  - [ ] `ImportPipeline` async methods need ConfigureAwait
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`, `src/Jaunty.FlatFiles.DuckDB/ImportPipeline/`

---

## P1: High Priority (Should Fix)

### API Design Consistency

- [x] **P1-7: Standardize method overloading pattern**
  - [x] Jaunty core uses optional parameters with defaults
  - [x] FlatFiles methods reviewed - pattern is consistent
  - [x] **Status: ACCEPTABLE** - FlatFiles uses consistent overloading

- [x] **P1-8: Add sync counterparts for async methods**
  - [x] Added sync `Insert<T>()`, `Insert<T>(IEnumerable<T>)` methods
  - [x] Added sync `Update<T>()`, `Delete<T>()` methods
  - [x] Added sync `Save<T>()`, `Save<T>(WriteBackMode)` methods
  - [x] Added sync `Export<T>()` method
  - [x] Sync methods wrap async with `.GetAwaiter().GetResult()` (consistent with .NET patterns)
  - **Files:** `src/Jaunty.FlatFiles/IFlatFile.cs`, `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`
  - **Status:** COMPLETE - All 279 tests pass

- [x] **P1-9: Fix generic type constraints**
  - [x] **RESOLVED: Finding was INCORRECT**
  - [x] FlatFiles intentionally uses `where T : class, new()` (not `where T : new()`)
  - [x] Rationale: Flat file entities are reference types; mutation tracking requires classes
  - [x] This is a deliberate design choice appropriate for the FlatFiles use case
  - **Files:** N/A - No changes needed

- [ ] **P1-10: Add `CommandOptions` support to FlatFiles API**
  - [ ] Jaunty core uses `CommandOptions` for transaction/timeout
  - [ ] FlatFiles CRUD methods don't support transactions
  - [ ] Add `CommandOptions` parameter to all CRUD methods
  - **Files:** `src/Jaunty.FlatFiles/IFlatFile.cs`, `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`

### Error Handling

- [ ] **P1-11: Use specific exception types consistently**
  - [ ] Some FlatFiles code throws generic `ArgumentException`
  - [ ] Should throw `ArgumentNullException`, `InvalidOperationException` with specific messages
  - [ ] Match Jaunty's exception message style (include type names, available options)
  - **Files:** All FlatFiles source files

- [ ] **P1-12: Add parameter validation at method entry**
  - [ ] Use `ArgumentNullException.ThrowIfNull` pattern consistently
  - [ ] Add validation for expression parameters
  - [ ] Include descriptive error messages
  - **Files:** All FlatFiles source files

### Performance & Allocations

- [ ] **P1-13: Pre-allocate collections with known capacity**
  - [ ] `DuckDb.QueryAsync<T>()` doesn't pre-size `List<T>`
  - [ ] `FlatFileExpressionHelper` should pre-size `StringBuilder`
  - [ ] Match Jaunty's allocation-avoidance patterns
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`, `src/Jaunty.FlatFiles.DuckDB/FlatFileExpressionHelper.cs`

- [ ] **P1-14: Cache expression compilation results**
  - [ ] `FlatFileExpressionHelper` caches some expressions but not all
  - [ ] `CreateGetter` and `CreateSetter` should cache delegates
  - [ ] Add caching for predicate translation
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/FlatFileExpressionHelper.cs`

- [ ] **P1-15: Use `FrozenDictionary` for .NET 8+**
  - [ ] Jaunty core uses `FrozenDictionary` for cached metadata
  - [ ] FlatFiles should use `FrozenDictionary` for column mappings
  - [ ] Match Jaunty's conditional compilation pattern
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/FlatFileExpressionHelper.cs`

---

## P2: Medium Priority (Recommended)

### Project Structure & Organization

- [ ] **P2-16: Align target frameworks**
  - [ ] Jaunty targets `netstandard2.0;net8.0`
  - [ ] Jaunty.FlatFiles.DuckDB targets only `net8.0`
  - [ ] Consider adding `netstandard2.0` target to FlatFiles abstractions
  - **Files:** `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj`, `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`

- [ ] **P2-17: Add InternalsVisibleTo attributes consistently**
  - [ ] Jaunty.FlatFiles.DuckDB needs InternalsVisibleTo for test project
  - [ ] Verify all cross-project visibility is correct
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`

- [ ] **P2-18: Standardize NuGet package metadata**
  - [ ] Add `<PackageTags>` to FlatFiles projects
  - [ ] Add `<PackageReadmeFile>` reference
  - [ ] Ensure version numbers match Jaunty core
  - **Files:** `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj`, `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`

### Testing & Quality

- [ ] **P2-19: Verify test framework consistency**
  - [ ] Jaunty core tests use xUnit Assert (FluentAssertions removed per CONTRIBUTING.md)
  - [ ] FlatFiles tests should match: use xUnit Assert only
  - [ ] Update test code if using FluentAssertions
  - **Files:** `tests/Jaunty.FlatFiles.Tests/`, `tests/Jaunty.FlatFiles.DuckDB.Tests/`

- [ ] **P2-20: Increase test coverage for edge cases**
  - [ ] Add tests for null handling in materialization
  - [ ] Add tests for expression translation edge cases
  - [ ] Add tests for transaction rollback scenarios
  - **Files:** `tests/Jaunty.FlatFiles.DuckDB.Tests/`

- [ ] **P2-21: Add performance benchmarks**
  - [ ] Benchmark FlatFiles query performance vs raw DuckDB
  - [ ] Benchmark import pipeline vs native bulk insert
  - [ ] Add regression tests to catch performance degradation
  - **Files:** `benchmarks/Jaunty.FlatFiles.Benchmarks/`

### Documentation

- [ ] **P2-22: Verify README.md files**
  - [ ] Jaunty.FlatFiles has README (good)
  - [ ] Jaunty.FlatFiles.DuckDB has README (good)
  - [ ] Add cross-references between packages
  - **Files:** `src/Jaunty.FlatFiles/README.md`, `src/Jaunty.FlatFiles.DuckDB/README.md`

- [ ] **P2-23: Update API documentation examples**
  - [ ] Add more practical examples to XML docs
  - [ ] Include error handling examples
  - [ ] Show transaction usage patterns
  - **Files:** All FlatFiles source files with XML docs

- [ ] **P2-24: Document DuckDB-specific quirks**
  - [ ] Document VIEW → TABLE promotion behavior
  - [ ] Document type mapping edge cases (DATE → TIMESTAMP)
  - [ ] Document platform support and NativeAOT limitations
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/README.md`

---

## P3: Low Priority (Nice to Have)

### Feature Parity

- [ ] **P3-25: Add streaming support for large result sets**
  - [ ] Jaunty core has `QueryStream<T>()` and `QueryStreamAsync<T>()`
  - [ ] FlatFiles could benefit from streaming for large files
  - [ ] Add `IAsyncEnumerable<T>` support
  - **Files:** `src/Jaunty.FlatFiles/IFlatFile.cs`, `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`

- [ ] **P3-26: Add multiple result set support**
  - [ ] Jaunty core has `QueryMultiple()` support
  - [ ] Consider adding to FlatFiles for complex queries
  - **Files:** `src/Jaunty.FlatFiles/IFlatFile.cs`, `src/Jaunty.FlatFiles.DuckDB/DuckDb.cs`

- [ ] **P3-27: Document stored procedure limitation**
  - [ ] Jaunty core has `ExecuteStoredProcedure` methods
  - [ ] Not applicable to FlatFiles (no stored procs in DuckDB)
  - [ ] Document this limitation
  - **Files:** `src/Jaunty.FlatFiles/README.md`

### Code Quality

- [ ] **P3-28: Enable nullable reference types for netstandard2.0**
  - [ ] Use `#nullable enable` annotations
  - [ ] Match Jaunty core's nullable patterns
  - **Files:** All FlatFiles source files

- [ ] **P3-29: Add analyzer rules for FlatFiles projects**
  - [ ] Enable same analyzers as Jaunty core
  - [ ] Add custom analyzers for FlatFiles-specific rules
  - **Files:** `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj`, `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`

- [ ] **P3-30: Refactor ImportPipeline for extensibility**
  - [ ] Make import dialects pluggable
  - [ ] Add support for more database types
  - [ ] Document extension points
  - **Files:** `src/Jaunty.FlatFiles.DuckDB/ImportPipeline/`

### Developer Experience

- [ ] **P3-31: Add source generator for FlatFiles**
  - [ ] Generate column mappings at compile time
  - [ ] Reduce runtime reflection
  - [ ] Improve NativeAOT compatibility
  - **Files:** New project `src/Jaunty.FlatFiles.SourceGenerator/`

- [ ] **P3-32: Add XML documentation file generation**
  - [ ] Enable `<GenerateDocumentationFile>` for all configurations
  - [ ] Currently only enabled for some target frameworks
  - **Files:** `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj`, `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`

- [ ] **P3-33: Create the coding standards for FlatFiles**
  - [ ] Create similar guide for FlatFiles implementation
  - [ ] Document architecture decisions
  - **Files:** the module's coding notes, the module's coding notes

---

## Execution Plan

### Phase 1 (Week 1-2): P0 Critical Fixes
- Focus: Documentation and code style fundamentals
- Deliverable: Build passes with no warnings, code follows Jaunty conventions

### Phase 2 (Week 3-4): P1 High Priority
- Focus: API consistency and error handling
- Deliverable: FlatFiles API matches Jaunty core patterns

### Phase 3 (Week 5-6): P2 Medium Priority
- Focus: Project structure and testing
- Deliverable: Complete test coverage, proper project organization

### Phase 4 (Future): P3 Enhancements
- Focus: Feature parity and developer experience
- Deliverable: Feature-complete with Jaunty core

---

## Progress Tracking

| Phase | Tasks | Completed | In Progress | Blocked |
|-------|-------|-----------|-------------|---------|
| P0 | 7 | 0 | 0 | 0 |
| P1 | 12 | 0 | 0 | 0 |
| P2 | 11 | 0 | 0 | 0 |
| P3 | 7 | 0 | 0 | 0 |

---

## References

- [Jaunty API Design Guidelines](docs/API-DESIGN.md)
- [Jaunty Code Review Checklist](docs/CODE-REVIEW.md)
- [Jaunty Contributing Guide](CONTRIBUTING.md)
- [Jaunty Architecture Decisions](docs/ARCHITECTURE-DECISIONS.md)
