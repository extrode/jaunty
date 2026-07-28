# Jaunty.FlatFiles — Milestones & Feature List

> milestones.md — All features categorized, prioritized, and organized into milestones.

---

## Priority Legend

| Priority | Meaning |
|---|---|
| P0 — Critical | Must have for the milestone to ship |
| P1 — Important | Should have; deferral needs justification |
| P2 — Nice to Have | Can defer to next milestone without impact |

## Category Legend

| Category | Scope |
|---|---|
| Infrastructure | Project setup, CI, packaging |
| Integration | DuckDB connectivity, ADO.NET, dialect |
| Read | Query operations against flat files |
| Write | INSERT, UPDATE, DELETE, write-back |
| Import | Bulk load into target databases |
| Testing | Test infrastructure, fixtures, benchmarks |
| Documentation | Docs, diagrams, README, API docs |

---

## Milestone 0: Foundation & DuckDB Integration

**Goal**: Stand up project structure, prove DuckDB.NET works with Jaunty's pipeline.
**Duration**: 3–4 days
**Branch**: `feature/flatfiles-m0-foundation`

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-001 | Create `Jaunty.FlatFiles` project (netstandard2.0 + net10.0) | build | P0 | Project compiles, added to solution, NuGet metadata set |
| F-002 | Create `Jaunty.FlatFiles.DuckDB` project (net8.0 + net10.0) | build | P0 | Project compiles, references DuckDB.NET.Data.Full |
| F-003 | Create test projects with xUnit + FluentAssertions | testing | P0 | `dotnet test` runs and passes on all TFMs |
| F-004 | Define core interfaces (`IFlatFileDatabase`, `IFileSource`, `IFlatFileDialect`) | build | P0 | Interfaces compile in abstractions package |
| F-005 | Define configuration types (`FlatFileDatabaseOptions`, per-format options) | build | P0 | All option classes with builder methods |
| F-006 | Implement `DuckDbDialect` scaffolding (IDialect) | yes | P0 | Identifier quoting, parameter prefix, type mapping |
| F-007 | Verify DuckDB.NET ADO.NET provider compatibility | yes | P0 | Open in-memory DuckDB, execute SQL, read via DbDataReader |
| F-008 | Verify Jaunty materialization works with DuckDB.NET's DbDataReader | yes | P0 | Raw SQL query materializes into C# entity correctly |
| F-009 | Commit test data fixtures (CSV, TSV, Parquet, JSON) | testing | P0 | All fixture files in test project, loadable |
| F-010 | Architecture diagrams (Mermaid + SVG) | docs | P1 | Package diagram, data flow diagram committed |

**Exit Gate**: A raw SQL query executed through Jaunty against in-memory DuckDB returns correctly materialized C# entities.

---

## Milestone 1: CSV/TSV Query Support

**Goal**: Open a CSV or TSV file and query it with Jaunty's fluent API end-to-end.
**Duration**: 3–5 days
**Branch**: `feature/flatfiles-m1-csv-tsv`
**Depends on**: M0

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-011 | `FlatFileDatabase.Open(string path)` — single file shorthand | read | P0 | Opens CSV, infers format from extension, returns IFlatFileDatabase |
| F-012 | `FlatFileDatabase.Open(Action<Options>)` — configured multi-source | read | P0 | Multiple files registered with per-source config |
| F-013 | `CsvFileSource` — generate `CREATE VIEW` with `read_csv_auto()` | read | P0 | VIEW created in DuckDB on file registration |
| F-014 | `TsvFileSource` — generate `CREATE VIEW` with `read_csv_auto(delim='\t')` | read | P0 | TSV loaded correctly with tab delimiter |
| F-015 | Entity-to-table name resolution (`[Table]` attribute or class name) | read | P0 | Entity maps to correct DuckDB view name |
| F-016 | `DuckDbDialect` — WHERE clause generation from expressions | read | P0 | `.Where(x => x.Revenue > 10000)` generates correct SQL |
| F-017 | `DuckDbDialect` — ORDER BY generation | read | P0 | `.OrderBy()` / `.OrderByDescending()` work |
| F-018 | `DuckDbDialect` — LIMIT/OFFSET generation | read | P0 | `.Take()` / `.Skip()` generate correct SQL |
| F-019 | `DuckDbDialect` — SELECT projection | read | P1 | `.Select(x => new { x.Name, x.Revenue })` works |
| F-020 | Schema validation — entity vs inferred file schema | read | P1 | Mismatch throws descriptive error with column name + expected/actual type |
| F-021 | CsvOptions full support (header, delimiter, null, skip, encoding) | read | P1 | All options reflected in generated SQL |
| F-022 | Test: query CSV with filters, sorting, pagination | testing | P0 | All query combinations pass |
| F-023 | Test: empty file handling | testing | P0 | Returns empty list, no exception |
| F-024 | Test: no-header CSV | testing | P1 | Works with explicit column mapping |
| F-025 | Test: column name mismatch | testing | P1 | Clear error message |
| F-026 | Test: large file (100K+ rows) | testing | P1 | Completes in reasonable time, no OOM |

**Exit Gate**: `FlatFileDatabase.Open("sales.csv")` → `.Query<SalesRecord>().Where(...).OrderBy(...).Take(50).ToListAsync()` returns correct results.

---

## Milestone 2: Parquet & JSON Query Support

**Goal**: Extend file source support to Parquet and JSON formats.
**Duration**: 2–3 days
**Branch**: `feature/flatfiles-m2-parquet-json`
**Depends on**: M1

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-027 | `ParquetFileSource` — `read_parquet()` view generation | read | P0 | Parquet file queryable through fluent API |
| F-028 | `JsonFileSource` — `read_json_auto()` view generation | read | P0 | JSON file queryable (both array and NDJSON) |
| F-029 | `ParquetOptions` (hive partitioning toggle) | read | P1 | Hive partitioning flag passed to DuckDB |
| F-030 | `JsonFileOptions` (format, max depth) | read | P1 | Format and depth options in generated SQL |
| F-031 | `PreloadIntoMemory` option (CREATE TABLE AS vs VIEW) | read | P0 | TABLE mode uses `CREATE TABLE AS`; subsequent queries faster |
| F-032 | Multi-source session (CSV + Parquet + JSON in one db) | read | P0 | Three file types open simultaneously, each queryable |
| F-033 | `GetSourceInfo<T>()` — expose file metadata | read | P2 | Returns path, format, row count, column info |
| F-034 | Test: Parquet queries with various column types | testing | P0 | All DuckDB↔C# type mappings validated |
| F-035 | Test: JSON array format | testing | P0 | Array JSON loads and queries correctly |
| F-036 | Test: NDJSON format | testing | P0 | Newline-delimited JSON loads correctly |
| F-037 | Test: multi-source mixed queries | testing | P0 | Query each source independently in same session |
| F-038 | Test: VIEW vs TABLE performance comparison | testing | P2 | Benchmark shows TABLE mode faster for repeated queries |

**Exit Gate**: All four formats (CSV, TSV, Parquet, JSON) queryable through one `FlatFileDatabase` instance.

---

## Milestone 3: CRUD & Write-Back

**Goal**: Enable INSERT, UPDATE, DELETE against flat file sources with write-back to disk.
**Duration**: 4–6 days
**Branch**: `feature/flatfiles-m3-crud-writeback`
**Depends on**: M2

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-039 | Table promotion logic (VIEW → TABLE on first mutation) | docs | P0 | Transparent promotion, no user action needed |
| F-040 | `InsertAsync<T>(entity)` — single row insert | docs | P0 | Row added to DuckDB table, queryable immediately |
| F-041 | `InsertAsync<T>(IEnumerable<T>)` — batch insert | docs | P0 | Multiple rows added efficiently |
| F-042 | `UpdateAsync<T>(predicate, column, value)` | docs | P0 | Matching rows updated in DuckDB |
| F-043 | `DeleteAsync<T>(predicate)` | docs | P0 | Matching rows removed from DuckDB |
| F-044 | `IsModified<T>()` — track mutation state | docs | P1 | Returns true after any INSERT/UPDATE/DELETE |
| F-045 | `SaveAsync<T>(string outputPath)` — non-destructive write-back | docs | P0 | Modified data written to new file via COPY TO |
| F-046 | `SaveAsync<T>(WriteBackMode.Overwrite)` — destructive write-back | docs | P0 | Original file replaced atomically (temp + rename) |
| F-047 | `ExportAsync<T>(string outputPath)` — cross-format export | docs | P1 | Export CSV source as Parquet, etc. |
| F-048 | Output format inference from file extension | docs | P1 | `.parquet` → Parquet, `.csv` → CSV, etc. |
| F-049 | `WriteBackOptions` builder (header, delimiter for CSV output) | docs | P1 | Options reflected in COPY TO SQL |
| F-050 | Unsaved changes warning on Dispose | docs | P2 | Log warning if IsModified and no SaveAsync called |
| F-051 | Test: INSERT then query returns new row | testing | P0 | Round-trip verified |
| F-052 | Test: UPDATE then query returns modified values | testing | P0 | Round-trip verified |
| F-053 | Test: DELETE then query excludes removed rows | testing | P0 | Round-trip verified |
| F-054 | Test: SaveAsync non-destructive — original unchanged | testing | P0 | Original file byte-identical after save |
| F-055 | Test: SaveAsync destructive — original replaced | testing | P0 | Original file updated with modifications |
| F-056 | Test: ExportAsync CSV → Parquet | testing | P1 | Parquet file readable and contains correct data |
| F-057 | Test: table promotion transparent to queries | testing | P0 | Query before and after mutation returns correct results |
| F-058 | Test: concurrent read during write-back | testing | P2 | No corruption; document single-writer constraint |

**Exit Gate**: Full INSERT/UPDATE/DELETE cycle with write-back to CSV and Parquet, both destructive and non-destructive modes.

---

## Milestone 4: Import Pipeline

**Goal**: Bulk import flat file data into Jaunty-managed relational databases.
**Duration**: 4–6 days
**Branch**: `feature/flatfiles-m4-import`
**Depends on**: M1 (does not require M3)

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-059 | `IImportPipeline` interface | import | P0 | Contract for reading from DuckDB, writing to target |
| F-060 | `ImportIntoAsync<T>(target, options)` — main import method | import | P0 | Reads from flat file, writes to target db |
| F-061 | `FlatFileImporter.ImportAsync<T>(path, connection)` — shorthand | import | P1 | One-liner import convenience method |
| F-062 | SQLite import optimizer (batched INSERT with transaction) | import | P0 | Uses single transaction, configurable batch size |
| F-063 | PostgreSQL import optimizer (Npgsql COPY binary) | import | P1 | Uses Npgsql's binary COPY for maximum throughput |
| F-064 | SQL Server import optimizer (SqlBulkCopy) | import | P1 | Uses SqlBulkCopy with column mappings |
| F-065 | `CreateTableIfMissing` option | import | P0 | Creates target table from entity mapping if not exists |
| F-066 | `ConflictStrategy.Error` | import | P0 | Throws on primary key conflict |
| F-067 | `ConflictStrategy.Skip` | import | P1 | Ignores conflicting rows, continues import |
| F-068 | `ConflictStrategy.Upsert` | import | P1 | Updates existing rows on conflict |
| F-069 | Progress reporting callback | import | P1 | `OnProgress(imported, total)` fires per batch |
| F-070 | Schema alignment validation (source → target) | import | P0 | Mismatch throws before import starts |
| F-071 | Test: CSV → SQLite import end-to-end | testing | P0 | All rows in target, types correct |
| F-072 | Test: Parquet → SQLite import | testing | P0 | Parquet source imports correctly |
| F-073 | Test: conflict strategies (error, skip, upsert) | testing | P0 | Each strategy behaves correctly |
| F-074 | Test: CreateTableIfMissing | testing | P0 | Table created with correct schema |
| F-075 | Test: progress callback fires | testing | P1 | Callback invoked with correct counts |
| F-076 | Test: large file import (100K rows) performance | testing | P2 | Benchmark within 1.5x of native bulk tool |

**Exit Gate**: CSV imported into SQLite with all conflict strategies working. PostgreSQL and SQL Server optimizers functional.

---

## Milestone 5: Polish, Docs & Release

**Goal**: Error handling, documentation, NuGet packaging, CI pipeline.
**Duration**: 2–3 days
**Branch**: `feature/flatfiles-m5-release`
**Depends on**: M3, M4

| ID | Feature | Category | Priority | Acceptance Criteria |
|---|---|---|---|---|
| F-077 | Comprehensive error messages (file not found, parse errors, type mismatches) | build | P0 | All error paths tested with descriptive messages |
| F-078 | XML documentation comments on all public APIs | docs | P0 | IntelliSense works for all public types/methods |
| F-079 | README.md for Jaunty.FlatFiles | docs | P0 | Quick start, API examples, configuration reference |
| F-080 | README.md for Jaunty.FlatFiles.DuckDB | docs | P0 | Installation, DuckDB version notes, platform support |
| F-081 | Architecture documentation with Mermaid diagrams | docs | P1 | Package diagram, data flow, CRUD lifecycle |
| F-082 | NuGet package metadata (.nuspec or .csproj properties) | build | P0 | Both packages publishable to NuGet |
| F-083 | CI pipeline (build + test on Windows/Linux/macOS × all TFMs) | build | P0 | GitHub Actions workflow passing |
| F-084 | Benchmark suite published in docs | testing | P1 | Query, import, and write-back benchmarks documented |
| F-085 | `PersistTo(path)` — optional DuckDB file persistence | read | P2 | DuckDB state persisted to disk for caching |
| F-086 | Dispose cleanup — close DuckDB, release file handles | build | P0 | No leaked resources after dispose |

**Exit Gate**: Both NuGet packages publishable. CI green on all platforms. README and API docs complete.

---

## Summary

| Milestone | Features | P0 | P1 | P2 | Duration |
|---|---|---|---|---|---|
| M0: Foundation | F-001 – F-010 | 9 | 1 | 0 | 3–4 days |
| M1: CSV/TSV | F-011 – F-026 | 10 | 5 | 1 | 3–5 days |
| M2: Parquet/JSON | F-027 – F-038 | 7 | 2 | 3 | 2–3 days |
| M3: CRUD & Write-Back | F-039 – F-058 | 12 | 5 | 3 | 4–6 days |
| M4: Import Pipeline | F-059 – F-076 | 8 | 6 | 2 | 4–6 days |
| M5: Polish & Release | F-077 – F-086 | 5 | 3 | 2 | 2–3 days |
| **Total** | **86 features** | **51** | **22** | **11** | **18–27 days** |

### Dependency Graph

```
M0 ──→ M1 ──→ M2 ──→ M3
              │            ↘
              └──→ M4 ──→ M5
```

M3 (CRUD) and M4 (Import) can be developed in parallel after M1/M2.
