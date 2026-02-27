# Jaunty Commercial Readiness: Actionable Tasklist

**Generated:** 2026-02-27
**Source:** `docs/COMMERCIAL-ANALYSIS-REPORT.md`
**Scope:** Technical completeness, quality, benchmarks, NuGet presence, documentation

---

## P0 -- Must Complete

### 1. BenchmarkDotNet Project
**Report ref:** Part 5.1 -- "No published benchmarks"
**Goal:** Prove performance claims with reproducible numbers vs Dapper.

- [ ] Create `benchmarks/Jaunty.Benchmarks/Jaunty.Benchmarks.csproj` (net8.0, BenchmarkDotNet)
- [ ] Add to `Jaunty.slnx` under `/benchmarks/` folder
- [ ] Benchmark: `Query<T>` -- 1, 10, 100, 1000 rows (Jaunty vs Dapper)
- [ ] Benchmark: `QueryFirst<T>` / `QuerySingle<T>` (Jaunty vs Dapper)
- [ ] Benchmark: `QueryScalar<T>` (Jaunty vs Dapper)
- [ ] Benchmark: `Insert<T>` single entity (Jaunty vs Dapper.Contrib)
- [ ] Benchmark: `BulkInsert<T>` 100/1000 rows (Jaunty only -- Dapper has no built-in bulk)
- [ ] Benchmark: Parameter binding -- named vs positional vs anonymous object
- [ ] Benchmark: Source-generated mapper vs reflection mapper (Jaunty internal comparison)
- [ ] Benchmark: `OrdinalMap` (new) vs `OrdinalCache` (old) -- demonstrate zero-alloc improvement
- [ ] Use SQLite in-memory for all benchmarks (no external DB dependency)
- [ ] Generate markdown results table for documentation
- [ ] Add `README.md` in benchmarks/ with instructions to run

**Files to create:**
- `benchmarks/Jaunty.Benchmarks/Jaunty.Benchmarks.csproj`
- `benchmarks/Jaunty.Benchmarks/Program.cs`
- `benchmarks/Jaunty.Benchmarks/Benchmarks/QueryBenchmarks.cs`
- `benchmarks/Jaunty.Benchmarks/Benchmarks/InsertBenchmarks.cs`
- `benchmarks/Jaunty.Benchmarks/Benchmarks/ParameterBenchmarks.cs`
- `benchmarks/Jaunty.Benchmarks/Benchmarks/MapperBenchmarks.cs`
- `benchmarks/Jaunty.Benchmarks/Entities/` (benchmark entity classes)

---

### 2. NuGet Package Metadata
**Report ref:** Part 5.1 -- "Zero community / NuGet presence"
**Goal:** Professional NuGet listings with proper metadata for discovery.

All 5 packages need metadata updates:

- [ ] `src/Jaunty/Jaunty.csproj` -- Expand `Description` from "A fast ORM" to meaningful description
- [ ] `src/Jaunty.Fluent/Jaunty.Fluent.csproj` -- Add missing metadata
- [ ] `src/Jaunty.Extensions.Reflection/Jaunty.Extensions.Reflection.csproj` -- Add missing metadata
- [ ] `src/Jaunty.Scaffolding/Jaunty.Scaffolding.csproj` -- Add missing metadata
- [ ] `src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj` -- Add missing metadata

**Properties to add to each `.csproj`:**
```xml
<PackageTags>orm;micro-orm;dapper;sql;database;aot;nativeaot;source-generator</PackageTags>
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageLicenseFile>LICENSE</PackageLicenseFile>  <!-- or PackageLicenseExpression -->
<PackageProjectUrl>https://github.com/beparey/jaunty</PackageProjectUrl>
<PackageReleaseNotes>Initial release</PackageReleaseNotes>
<Copyright>Copyright (c) 2026 Syed Beparey</Copyright>
```

- [ ] Create a proper top-level `README.md` suitable for NuGet (if not already suitable)
- [ ] Add `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` to each .csproj
- [ ] Create or verify `LICENSE` file exists at repo root
- [ ] Add `<None Include="..\..\LICENSE" Pack="true" PackagePath="\" />` to each .csproj
- [ ] *(Optional)* Create a package icon (`icon.png`, 128x128) and add `PackageIcon`

---

### 3. Source Generator Bug Fix -- Nullable Type Cast
**Report ref:** Part 2.8 (implicit -- source gen rated B)
**Discovered during:** P1 NativeAOT sample project work

The `GetReaderMethod` fallback generates broken code for nullable/unknown types:
```csharp
// Current (buggy):
_ => "((\" + typeName + \")reader.GetValue)"
// Generates: ((string?)reader.GetValue)(ord[0])  -- treats GetValue as method group

// Fix:
_ => "((" + typeName + ")reader.GetValue(ord[INDEX]))"
// Or restructure to emit the full call inline
```

**File:** `src/Jaunty.SourceGenerator/JauntyGenerator.cs` -- `GetReaderMethod()` (line ~199)

- [ ] Fix `GetReaderMethod` to handle nullable types (`string?`, `DateTime?`, etc.) correctly
- [ ] Add `System.String` to the switch cases (Roslyn sometimes emits `System.String` not `string`)
- [ ] Add `System.DateTime` and `System.Guid` full-name variants
- [ ] Verify fix with a sample entity that has `string?` and `DateTime?` properties
- [ ] Run full test suite to confirm no regressions

---

## P1 -- Important

### 4. Fluent API: 3-Way Join Completion
**Report ref:** Part 5.1 -- "3-way join limitations in Fluent"
**Current state:** `IJoinedQuery3<T1,T2,T3>` missing async methods, OrderBy, pagination

`IJoinedQuery3` currently has only:
```csharp
IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate);
IJoinedQuery3<T1, T2, T3> Where(string condition);
List<T1> Select();
List<(T1, T2, T3)> SelectAll();
string ToSql();
```

- [ ] Add `OrderBy<TKey>(Expression<Func<T1, TKey>> selector)` to `IJoinedQuery3`
- [ ] Add `OrderByDescending<TKey>(Expression<Func<T1, TKey>> selector)` to `IJoinedQuery3`
- [ ] Add `Take(int count)` and `Skip(int count)` to `IJoinedQuery3`
- [ ] Add `Task<List<T1>> SelectAsync(CancellationToken ct = default)` to `IJoinedQuery3`
- [ ] Add `Task<List<(T1,T2,T3)>> SelectAllAsync(CancellationToken ct = default)` to `IJoinedQuery3`
- [ ] Implement all new methods in `JoinedQuery3Builder<T1,T2,T3>`
- [ ] Add tests for 3-way join ordering, pagination, and async execution

**Files:**
- `src/Jaunty.Fluent/Interfaces/IJoinedQuery.cs` -- interface additions
- `src/Jaunty.Fluent/JoinedQueryBuilder.cs` -- implementation (~line 1277)
- `tests/Jaunty.Fluent.Tests/` -- new test file

---

### 5. Fluent API: CTE Async Gaps
**Report ref:** Part 5.2 -- "Fluent API CTE async gaps"
**Current state:** `ICteQueryClause<T>` has `SelectAsync()` but missing `SelectFirstAsync()` and `SelectFirstOrDefaultAsync()`

- [ ] Add `Task<T> SelectFirstAsync(CancellationToken ct = default)` to `ICteQueryClause<T>`
- [ ] Add `Task<T?> SelectFirstOrDefaultAsync(CancellationToken ct = default)` to `ICteQueryClause<T>`
- [ ] Implement in `CteBuilder<T>`
- [ ] Add tests for CTE async first/firstOrDefault queries

**Files:**
- `src/Jaunty.Fluent/Interfaces/ICteClause.cs` -- interface additions
- `src/Jaunty.Fluent/CteBuilder.cs` -- implementation
- `tests/Jaunty.Fluent.Tests/` -- new/extended test file

---

### 6. Navigation Property Generation in Scaffolding
**Report ref:** Part 5.2 -- "No navigation property generation"
**Current state:** `ForeignKeyInfo` is detected but not used during code generation

- [ ] Extend `EntityCodeGenerator.GenerateEntity()` to emit navigation properties from FK data
- [ ] Generate collection navigation properties on the "one" side (`ICollection<Child>`)
- [ ] Generate reference navigation properties on the "many" side (`Parent Parent { get; set; }`)
- [ ] Add `[ForeignKey]` attribute annotations
- [ ] Make navigation property generation opt-in via scaffolding config/CLI flag
- [ ] Add scaffolding tests for navigation property output
- [ ] Update Scaffolding CLI help text

**Files:**
- `src/Jaunty.Scaffolding/CodeGeneration/EntityCodeGenerator.cs`
- `src/Jaunty.Scaffolding.Cli/Commands/ScaffoldCommand.cs` (new option)
- `tests/Jaunty.Scaffolding.Tests/`

---

### 7. Fluent API Test Coverage (33% -> 70%+)
**Report ref:** Part 7.2 -- "Push Fluent API coverage from 33% to 70%+"
**Current state:** 679 tests passing, 33% line coverage (6199 statements)

- [ ] Identify uncovered areas using existing coverage report in `docs/code-coverage/`
- [ ] Add tests for uncovered JOIN scenarios (outer joins, self-joins, cross-joins)
- [ ] Add tests for uncovered WHERE clause edge cases (LIKE, IS NULL, IN with empty list)
- [ ] Add tests for uncovered aggregate functions (SUM, AVG, MIN, MAX, COUNT with GROUP BY)
- [ ] Add tests for UNION/INTERSECT/EXCEPT edge cases
- [ ] Add tests for Window functions (ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD)
- [ ] Add tests for CASE WHEN expressions
- [ ] Add tests for fluent INSERT/UPDATE/DELETE operations
- [ ] Add tests for dialect-specific SQL generation (SQL Server vs PostgreSQL vs MySQL vs SQLite)
- [ ] Re-run coverage, target 70%+ line coverage

---

### 8. GridReader Async Test Coverage
**Report ref:** Part 7.2 -- "Add explicit GridReader async method tests"
**Current state:** GridReader async methods tested indirectly

Recent work added `GridReaderAsyncTests.cs` and `GridReaderTests.cs` (shown in git status as modified). Verify these are sufficient.

- [ ] Review `tests/Jaunty.Tests/Integration/Multiple/GridReaderAsyncTests.cs` for completeness
- [ ] Review `tests/Jaunty.Tests/Integration/Multiple/GridReaderTests.cs` for completeness
- [ ] Add tests for: ReadAsync<T> with custom mapper, ReadAsync with empty result sets
- [ ] Add tests for: multiple ReadAsync calls on same GridReader (sequential consumption)
- [ ] Add tests for: GridReader disposal and error handling
- [ ] Add tests for: CancellationToken support in async GridReader methods

---

## P2 -- Medium Priority

### 9. Documentation Website
**Report ref:** Part 7.3 -- "API reference website"
**Current state:** Rich docs exist in `docs/` but no publishing pipeline

- [ ] Choose documentation framework (DocFX for API reference, or MkDocs Material for guides)
- [ ] Create configuration file (`docfx.json` or `mkdocs.yml`)
- [ ] Set up GitHub Pages deployment (GitHub Actions workflow)
- [ ] Organize existing docs into navigation structure:
  - Getting Started (`docs/00-quick-start/`)
  - API Reference (`docs/01-api-reference/`)
  - Architecture (`docs/02-architecture/`)
  - Development (`docs/03-development/`)
  - NativeAOT Guide (`docs/archive/2026-02-nativeaot-migration/NATIVEAOT-GUIDE.md`)
- [ ] Write a "Getting Started" tutorial with step-by-step example
- [ ] Write migration guides: "From Dapper to Jaunty" and "From EF Core to Jaunty"
- [ ] Add benchmark results page (after task 1 is complete)

---

### 10. CI/CD Pipeline Enhancements
**Report ref:** Part 7.2 -- "Add multi-database CI matrix"
**Current state:** CI runs SQLite tests only

- [ ] Add Docker service containers for SQL Server and PostgreSQL to CI workflow
- [ ] Run full test suite (not just SQLite filter) in CI with database containers
- [ ] Add code coverage reporting step (Coverlet + upload to Codecov or similar)
- [ ] Add benchmark run step (optional, nightly schedule to avoid slowing PRs)

**File:** `.github/workflows/ci.yml`

---

### 11. NuGet Package Description Improvements
**Report ref:** Part 5.1 -- relates to NuGet presence

Write compelling, keyword-rich descriptions for each package:

- [ ] **Beparey.Jaunty**: "High-performance micro-ORM for .NET with strict mapping validation, source-generated AOT-compatible mappers, positional parameters, and complete CRUD operations. Supports SQL Server, PostgreSQL, MySQL, and SQLite."
- [ ] **Beparey.Jaunty.Fluent**: "Type-safe fluent SQL query builder for Jaunty ORM. Build SELECT, JOIN, WHERE, GROUP BY, ORDER BY, UNION, CTE, and Window function queries with compile-time safety."
- [ ] **Beparey.Jaunty.Extensions.Reflection**: "Optional reflection-based mapping extension for Jaunty ORM. Enables Dictionary, KeyValuePair, ValueTuple, and ExpandoObject mapping. Required for NativeAOT special type support."
- [ ] **Beparey.Jaunty.Scaffolding**: "Database-first code generation library for Jaunty ORM. Generates C# entity classes from SQL Server, PostgreSQL, MySQL, and SQLite schemas."
- [ ] **Beparey.Jaunty.Scaffolding.Cli**: "CLI tool for Jaunty ORM scaffolding. Generate entity classes from database schema. Install as .NET tool: dotnet tool install Beparey.Jaunty.Scaffolding.Cli"

---

## P3 -- Nice to Have (Future)

### 12. Composite Primary Key Support
**Report ref:** Part 5.2
- [ ] Support multiple `[Key]` attributes per entity
- [ ] Update source generator to emit composite key handling in BindUpdate/BindDelete
- [ ] Update `CrudSqlCache` to build WHERE clauses with multiple key columns
- [ ] Update Upsert to use composite key for conflict detection
- [ ] Add tests with composite key entities

### 13. Query Profiling / Slow Query Detection
**Report ref:** Part 5.2
- [ ] Add `JauntyConfig.SlowQueryThreshold` (TimeSpan)
- [ ] Add `JauntyConfig.SlowQueryHandler` (Action<string, TimeSpan, object?>)
- [ ] Instrument `ExecuteReader`/`ExecuteReaderAsync` with Stopwatch timing
- [ ] Fire handler when query exceeds threshold
- [ ] Add tests for slow query detection

### 14. Connection Resilience / Retry
**Report ref:** Part 5.2
- [ ] Add `JauntyConfig.RetryPolicy` with configurable max retries and backoff
- [ ] Implement exponential backoff with jitter
- [ ] Detect transient exceptions per database provider
- [ ] Wrap connection open + command execute in retry loop
- [ ] Add tests with simulated transient failures

---

## Already Complete (From NativeAOT Work)

These items from the report are already done:

- [x] **CI/CD pipeline** -- `.github/workflows/ci.yml` with build, test, AOT verification
- [x] **NativeAOT source generation** -- Source generator with zero-reflection mapping
- [x] **Sample projects** -- 3 NativeAOT samples in `samples/`
- [x] **Zero-allocation hot path** -- OrdinalMap replaces per-row Dictionary allocation
- [x] **PublishAot in Scaffolding CLI** -- Added to .csproj
- [x] **Fluent API test fixes** -- All 679 tests passing (0 failures)

---

## Execution Order Recommendation

```
P0 Tasks (do first):
  3. Source gen bug fix (small, high-value -- unblocks nullable entity properties)
  2. NuGet metadata (small, mechanical -- improves discoverability immediately)
  1. Benchmarks project (medium, high-value -- proves performance claims)

P1 Tasks (do second):
  5. CTE async gaps (small, completes API surface)
  4. 3-way join completion (medium, completes API surface)
  8. GridReader async tests (small, validates existing work)
  7. Fluent test coverage (large, ongoing)
  6. Navigation properties (medium, improves scaffolding story)

P2 Tasks (do third):
  11. Package descriptions (small, mechanical)
  9. Documentation website (medium-large, ongoing)
  10. CI enhancements (medium)

P3 Tasks (backlog):
  12-14. Future features as demand warrants
```
