# Jaunty.FlatFiles — Tasks

> tasks.md — Actionable task breakdown by milestone. Each task is a single commit.

---

## Task Format

```
[T###] Title
  Category: ...
  Priority: P0/P1/P2
  Feature: F-###
  Depends: T### (if any)
  Files: list of files to create/modify
  Tests: list of test files/methods
  Acceptance: testable assertion
```

---

## Milestone 0: Foundation & DuckDB Integration

### [T001] Create Jaunty.FlatFiles project
- Category: Infrastructure
- Priority: P0
- Feature: F-001
- Files:
  - `src/Jaunty.FlatFiles/Jaunty.FlatFiles.csproj`
  - Update `Jaunty.sln`
- Acceptance: `dotnet build` succeeds targeting netstandard2.0 and net10.0

### [T002] Create Jaunty.FlatFiles.DuckDB project
- Category: Infrastructure
- Priority: P0
- Feature: F-002
- Depends: T001
- Files:
  - `src/Jaunty.FlatFiles.DuckDB/Jaunty.FlatFiles.DuckDB.csproj`
  - Update `Jaunty.sln`
- Acceptance: `dotnet build` succeeds targeting net8.0 and net10.0; DuckDB.NET.Data.Full referenced

### [T003] Create test projects
- Category: Testing
- Priority: P0
- Feature: F-003
- Depends: T001, T002
- Files:
  - `tests/Jaunty.FlatFiles.Tests/Jaunty.FlatFiles.Tests.csproj`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Jaunty.FlatFiles.DuckDB.Tests.csproj`
- Acceptance: `dotnet test` runs (0 tests, 0 failures) on all TFMs

### [T004] Define core interfaces
- Category: Infrastructure
- Priority: P0
- Feature: F-004
- Depends: T001
- Files:
  - `src/Jaunty.FlatFiles/IFlatFileDatabase.cs`
  - `src/Jaunty.FlatFiles/IFileSource.cs`
  - `src/Jaunty.FlatFiles/IFlatFileDialect.cs`
  - `src/Jaunty.FlatFiles/FileFormat.cs`
  - `src/Jaunty.FlatFiles/WriteBack/WriteBackMode.cs`
  - `src/Jaunty.FlatFiles/WriteBack/ExportFormat.cs`
  - `src/Jaunty.FlatFiles/WriteBack/IWriteBackStrategy.cs`
  - `src/Jaunty.FlatFiles/Import/IImportPipeline.cs`
  - `src/Jaunty.FlatFiles/Import/ConflictStrategy.cs`
- Tests: Compile-only validation
- Acceptance: All interfaces compile; no external dependencies beyond Jaunty core

### [T005] Define configuration types
- Category: Infrastructure
- Priority: P0
- Feature: F-005
- Depends: T004
- Files:
  - `src/Jaunty.FlatFiles/Configuration/FlatFileDatabaseOptions.cs`
  - `src/Jaunty.FlatFiles/Configuration/CsvOptions.cs`
  - `src/Jaunty.FlatFiles/Configuration/TsvOptions.cs`
  - `src/Jaunty.FlatFiles/Configuration/ParquetOptions.cs`
  - `src/Jaunty.FlatFiles/Configuration/JsonFileOptions.cs`
  - `src/Jaunty.FlatFiles/Configuration/WriteBackOptions.cs`
  - `src/Jaunty.FlatFiles/Import/ImportOptions.cs`
- Tests:
  - `tests/Jaunty.FlatFiles.Tests/Configuration/FlatFileDatabaseOptionsTests.cs`
  - Test builder methods return self (fluent chain)
  - Test defaults are applied
- Acceptance: Options build correctly; defaults match spec

### [T006] Implement DuckDbDialect scaffolding
- Category: Integration
- Priority: P0
- Feature: F-006
- Depends: T004
- Files:
  - `src/Jaunty.FlatFiles.DuckDB/DuckDbDialect.cs`
- Tests:
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/DuckDbDialectTests.cs`
  - Test: identifier quoting (`"column_name"`)
  - Test: parameter prefix (`$1`, `$2`)
  - Test: boolean literals (`true`/`false`)
  - Test: type mapping (C# → DuckDB)
  - Test: string concatenation operator (`||`)
- Acceptance: All dialect unit tests pass

### [T007] Verify DuckDB.NET ADO.NET compatibility
- Category: Integration
- Priority: P0
- Feature: F-007
- Depends: T002
- Files:
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/DuckDbConnectionTests.cs`
- Tests:
  - Test: open in-memory DuckDBConnection
  - Test: execute CREATE TABLE + INSERT via DuckDBCommand
  - Test: read results via DbDataReader
  - Test: parameter binding
  - Test: NULL handling
  - Test: transaction support
- Acceptance: All ADO.NET operations work as expected; document any quirks

### [T008] Verify Jaunty materialization with DuckDB
- Category: Integration
- Priority: P0
- Feature: F-008
- Depends: T006, T007
- Files:
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/MaterializationTests.cs`
- Tests:
  - Test: raw SQL → entity mapping via Jaunty's existing pipeline
  - Test: all C# ↔ DuckDB type mappings (string, int, decimal, DateTime, bool, etc.)
  - Test: nullable types
  - Test: column name mapping via `[Column]` attribute
- Acceptance: Jaunty materializes DuckDB query results into entities correctly

### [T009] Commit test data fixtures
- Category: Testing
- Priority: P0
- Feature: F-009
- Files:
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/sales.csv`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/sales.tsv`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/inventory.parquet`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/customers.json`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/customers.ndjson`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/empty.csv`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/no-header.csv`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/malformed.csv`
  - `tests/Jaunty.FlatFiles.DuckDB.Tests/Fixtures/TestEntities.cs`
- Acceptance: All fixtures loadable; Parquet generated via DuckDB COPY TO in a setup script

### [T010] Create architecture diagrams
- Category: Documentation
- Priority: P1
- Feature: F-010
- Files:
  - `docs/architecture/package-diagram.mermaid`
  - `docs/architecture/data-flow.mermaid`
  - `docs/architecture/crud-lifecycle.mermaid`
  - `docs/architecture/import-pipeline.mermaid`
  - `docs/architecture/package-diagram.svg`
- Acceptance: Diagrams render correctly; committed to docs/

---

## Milestone 1: CSV/TSV Query Support

### [T011] Implement FlatFileDatabase.Open(string path) — single file shorthand
- Priority: P0 | Feature: F-011 | Depends: T006, T008
- Files: `src/Jaunty.FlatFiles.DuckDB/DuckDbFlatFileDatabase.cs`
- Tests: Open CSV → query returns results
- Acceptance: Single-argument Open works for CSV files

### [T012] Implement FlatFileDatabase.Open(Action<Options>) — multi-source
- Priority: P0 | Feature: F-012 | Depends: T011
- Files: Update `DuckDbFlatFileDatabase.cs`
- Tests: Register 2+ sources, query each independently
- Acceptance: Builder-configured multi-source Open works

### [T013] Implement CsvFileSource with CREATE VIEW generation
- Priority: P0 | Feature: F-013 | Depends: T011
- Files: `src/Jaunty.FlatFiles.DuckDB/Sources/DuckDbCsvSource.cs`
- Tests: Verify generated SQL matches expected `read_csv_auto()` call
- Acceptance: CSV file registered as DuckDB VIEW

### [T014] Implement TsvFileSource
- Priority: P0 | Feature: F-014 | Depends: T013
- Files: `src/Jaunty.FlatFiles.DuckDB/Sources/DuckDbTsvSource.cs`
- Tests: TSV file loads with `delim='\t'`
- Acceptance: TSV queryable

### [T015] Entity-to-table name resolution
- Priority: P0 | Feature: F-015 | Depends: T013
- Tests: `[Table("custom_name")]` maps to view name; class name used as fallback
- Acceptance: Entity mapping drives view naming

### [T016] DuckDbDialect — WHERE clause from expressions
- Priority: P0 | Feature: F-016 | Depends: T006
- Tests: `.Where(x => x.Revenue > 10000)` → `WHERE "Revenue" > $1`; compound expressions; null checks
- Acceptance: All common expression types generate correct SQL

### [T017] DuckDbDialect — ORDER BY generation
- Priority: P0 | Feature: F-017 | Depends: T016
- Tests: `.OrderBy(x => x.Date)` → `ORDER BY "Date" ASC`; descending; multiple columns
- Acceptance: Ordering SQL correct

### [T018] DuckDbDialect — LIMIT/OFFSET
- Priority: P0 | Feature: F-018 | Depends: T016
- Tests: `.Take(50)` → `LIMIT 50`; `.Skip(10).Take(50)` → `LIMIT 50 OFFSET 10`
- Acceptance: Pagination SQL correct

### [T019] DuckDbDialect — SELECT projection
- Priority: P1 | Feature: F-019 | Depends: T016
- Tests: `.Select(x => new { x.Name, x.Revenue })` → `SELECT "Name", "Revenue"`
- Acceptance: Projection SQL correct

### [T020] Schema validation on registration
- Priority: P1 | Feature: F-020 | Depends: T013
- Tests: Entity with `int` column mapped to DuckDB `VARCHAR` → descriptive error
- Acceptance: Mismatch caught at registration, not query time

### [T021] CsvOptions full support
- Priority: P1 | Feature: F-021 | Depends: T013
- Tests: Each option (header, delimiter, null, skip, encoding) reflected in SQL
- Acceptance: All CSV configuration options functional

### [T022] Test suite — CSV query combinations
- Priority: P0 | Feature: F-022 | Depends: T016–T018
- Tests: Filter + sort + page; filter only; sort only; page only; all together
- Acceptance: All combinations return correct results

### [T023] Test — empty file handling
- Priority: P0 | Feature: F-023 | Depends: T013
- Tests: Open empty.csv → `.ToListAsync()` returns empty list
- Acceptance: No exception on empty file

### [T024] Test — no-header CSV
- Priority: P1 | Feature: F-024 | Depends: T021
- Tests: Open no-header.csv with `HasHeader = false` → works with positional mapping
- Acceptance: No-header files queryable

### [T025] Test — column name mismatch
- Priority: P1 | Feature: F-025 | Depends: T020
- Tests: Entity column "ProductName" vs CSV column "product_name" → error or `[Column]` resolution
- Acceptance: Clear error or successful mapping via attribute

### [T026] Test — large file (100K rows)
- Priority: P1 | Feature: F-026 | Depends: T013
- Tests: Generate 100K row CSV in test setup → query with filter → verify performance
- Acceptance: Completes in < 5 seconds; no OutOfMemoryException

---

## Milestone 2: Parquet & JSON Query Support

### [T027] Implement ParquetFileSource
- Priority: P0 | Feature: F-027 | Depends: T013
- Files: `src/Jaunty.FlatFiles.DuckDB/Sources/DuckDbParquetSource.cs`
- Tests: Parquet file registered and queryable

### [T028] Implement JsonFileSource
- Priority: P0 | Feature: F-028 | Depends: T013
- Files: `src/Jaunty.FlatFiles.DuckDB/Sources/DuckDbJsonSource.cs`
- Tests: JSON array and NDJSON both loadable

### [T029] ParquetOptions (hive partitioning)
- Priority: P1 | Feature: F-029 | Depends: T027
- Tests: `HivePartitioning = true` reflected in SQL

### [T030] JsonFileOptions (format, depth)
- Priority: P1 | Feature: F-030 | Depends: T028
- Tests: Format and MaxDepth options in generated SQL

### [T031] PreloadIntoMemory option
- Priority: P0 | Feature: F-031 | Depends: T027
- Tests: `PreloadIntoMemory = true` generates `CREATE TABLE AS` instead of `CREATE VIEW`

### [T032] Multi-source session test
- Priority: P0 | Feature: F-032 | Depends: T027, T028
- Tests: Open CSV + Parquet + JSON → query each → all return correct results

### [T033] GetSourceInfo<T>() metadata
- Priority: P2 | Feature: F-033 | Depends: T032
- Tests: Returns file path, format, estimated row count

### [T034–T038] Format-specific test suites
- Priority: P0–P2 | Features: F-034–F-038
- Tests: Type mapping, format variations, performance comparison

---

## Milestone 3: CRUD & Write-Back

### [T039] Table promotion (VIEW → TABLE)
- Priority: P0 | Feature: F-039 | Depends: T031
- Tests: First INSERT triggers DROP VIEW + CREATE TABLE AS; subsequent queries still work

### [T040] InsertAsync<T>(entity)
- Priority: P0 | Feature: F-040 | Depends: T039
- Tests: Insert row → query → row present

### [T041] InsertAsync<T>(IEnumerable<T>) batch
- Priority: P0 | Feature: F-041 | Depends: T040
- Tests: Insert 100 rows → query → all present

### [T042] UpdateAsync<T>
- Priority: P0 | Feature: F-042 | Depends: T039
- Tests: Update matching rows → query → values changed

### [T043] DeleteAsync<T>
- Priority: P0 | Feature: F-043 | Depends: T039
- Tests: Delete matching rows → query → rows gone; returns count

### [T044] IsModified<T>() tracking
- Priority: P1 | Feature: F-044 | Depends: T040
- Tests: False before mutation; true after INSERT/UPDATE/DELETE

### [T045] SaveAsync non-destructive
- Priority: P0 | Feature: F-045 | Depends: T040
- Tests: Save to new file → original unchanged → new file has modifications

### [T046] SaveAsync destructive (overwrite)
- Priority: P0 | Feature: F-046 | Depends: T045
- Tests: Save with Overwrite → original replaced; atomic (temp + rename)

### [T047] ExportAsync cross-format
- Priority: P1 | Feature: F-047 | Depends: T045
- Tests: CSV source → export as Parquet → reload Parquet → data matches

### [T048] Output format inference
- Priority: P1 | Feature: F-048 | Depends: T047
- Tests: `.parquet` extension → Parquet format auto-selected

### [T049] WriteBackOptions builder
- Priority: P1 | Feature: F-049 | Depends: T045
- Tests: Custom delimiter, header toggle reflected in output

### [T050] Unsaved changes warning
- Priority: P2 | Feature: F-050 | Depends: T044
- Tests: Dispose after mutation without Save → warning logged

### [T051–T058] CRUD test suite
- Priority: P0–P2 | Features: F-051–F-058
- Tests: Full round-trip tests for all CRUD operations + write-back modes

---

## Milestone 4: Import Pipeline

### [T059] IImportPipeline interface
- Priority: P0 | Feature: F-059 | Depends: T004

### [T060] ImportIntoAsync<T> main method
- Priority: P0 | Feature: F-060 | Depends: T059
- Tests: CSV → in-memory SQLite → verify all rows

### [T061] FlatFileImporter shorthand
- Priority: P1 | Feature: F-061 | Depends: T060
- Tests: One-liner import compiles and works

### [T062] SQLite import optimizer
- Priority: P0 | Feature: F-062 | Depends: T060
- Tests: Batched INSERT within transaction; correct batch size

### [T063] PostgreSQL import optimizer
- Priority: P1 | Feature: F-063 | Depends: T060
- Tests: Npgsql COPY binary protocol used (requires Postgres fixture or mock)

### [T064] SQL Server import optimizer
- Priority: P1 | Feature: F-064 | Depends: T060
- Tests: SqlBulkCopy used with column mappings

### [T065] CreateTableIfMissing
- Priority: P0 | Feature: F-065 | Depends: T060
- Tests: Import into non-existent table → table created with correct schema

### [T066–T068] Conflict strategies
- Priority: P0/P1 | Features: F-066–F-068
- Tests: Each strategy tested with duplicate key scenarios

### [T069] Progress reporting
- Priority: P1 | Feature: F-069 | Depends: T060
- Tests: Callback fires with correct counts

### [T070] Schema alignment validation
- Priority: P0 | Feature: F-070 | Depends: T060
- Tests: Mismatched source/target schema → error before any rows imported

### [T071–T076] Import test suite
- Priority: P0–P2 | Features: F-071–F-076

---

## Milestone 5: Polish & Release

### [T077] Error message audit
- Priority: P0 | Feature: F-077
- Tests: Every error path has descriptive message with file path and context

### [T078] XML documentation comments
- Priority: P0 | Feature: F-078
- Acceptance: Zero missing XML doc warnings on public members

### [T079–T080] README files
- Priority: P0 | Features: F-079–F-080

### [T081] Architecture docs with Mermaid
- Priority: P1 | Feature: F-081

### [T082] NuGet package metadata
- Priority: P0 | Feature: F-082
- Tests: `dotnet pack` produces valid .nupkg files

### [T083] CI pipeline
- Priority: P0 | Feature: F-083
- Files: `.github/workflows/flatfiles-ci.yml`
- Tests: Build + test on Windows/Linux/macOS × all TFMs

### [T084] Benchmark suite
- Priority: P1 | Feature: F-084
- Tests: BenchmarkDotNet results for query, import, write-back

### [T085] PersistTo option
- Priority: P2 | Feature: F-085
- Tests: DuckDB state saved to file; reloadable

### [T086] Dispose cleanup verification
- Priority: P0 | Feature: F-086
- Tests: After Dispose: DuckDB connection closed, no leaked file handles
