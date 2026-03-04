# Jaunty.FlatFiles Architecture

This document describes the architecture of the Jaunty.FlatFiles extension, including package structure, data flow, and key design decisions.

## Package Structure

```
Jaunty.sln
├── src/
│   ├── Jaunty/                          # Core micro-ORM (unchanged)
│   ├── Jaunty.FlatFiles/                # Abstractions package
│   │   ├── IFlatFileDatabase.cs         # Main database interface
│   │   ├── IFileSource.cs               # File source abstraction
│   │   ├── IFlatFileDialect.cs          # SQL dialect extension
│   │   ├── FileFormat.cs                # Format enum (Csv, Tsv, Parquet, Json)
│   │   ├── Configuration/
│   │   │   ├── FlatFileDatabaseOptions.cs
│   │   │   ├── CsvFileSource.cs         # CSV configuration
│   │   │   ├── TsvFileSource.cs         # TSV configuration
│   │   │   ├── ParquetFileSource.cs     # Parquet configuration
│   │   │   └── JsonFileSource.cs        # JSON configuration
│   │   ├── Import/
│   │   │   ├── IImportDialect.cs        # Import dialect interface
│   │   │   ├── ImportOptions.cs         # Import configuration
│   │   │   └── ConflictStrategy.cs      # Conflict resolution enum
│   │   └── WriteBackMode.cs             # Write-back mode enum
│   │
│   └── Jaunty.FlatFiles.DuckDB/         # DuckDB engine implementation
│       ├── DuckDbFlatFileDatabase.cs    # Main database implementation
│       ├── DuckDbDialect.cs             # DuckDB SQL dialect
│       ├── FlatFileDatabase.cs          # Static factory
│       ├── FlatFileImporter.cs          # Import convenience methods
│       ├── FlatFileExpressionHelper.cs  # Expression-to-SQL translator
│       └── ImportPipeline/
│           ├── ImportExecutor.cs        # Import execution engine
│           ├── ImportDialectResolver.cs # Dialect auto-detection
│           ├── TargetDdlGenerator.cs    # CREATE TABLE generation
│           ├── SqliteImportDialect.cs   # SQLite import dialect
│           ├── PostgreSqlImportDialect.cs # PostgreSQL import dialect
│           └── SqlServerImportDialect.cs # SQL Server import dialect
│
└── tests/
    ├── Jaunty.FlatFiles.Tests/          # Unit tests (abstractions)
    └── Jaunty.FlatFiles.DuckDB.Tests/   # Integration tests (DuckDB)
```

## Package Responsibilities

### Jaunty.FlatFiles (Abstractions)

**Targets:** `netstandard2.0`, `net8.0`
**Dependencies:** Jaunty core only
**NativeAOT:** Fully compatible

This package defines:
- Core interfaces (`IFlatFileDatabase`, `IFileSource`, `IFlatFileDialect`)
- Configuration types (options, file sources)
- Import pipeline interfaces
- Write-back mode enums

**Design Principle:** Zero dependencies on DuckDB.NET. All DuckDB-specific code lives in the engine package.

### Jaunty.FlatFiles.DuckDB (Engine)

**Targets:** `net8.0`
**Dependencies:** Jaunty.FlatFiles + DuckDB.NET.Data.Full
**NativeAOT:** Best-effort (depends on DuckDB.NET)

This package implements:
- `DuckDbFlatFileDatabase` - Main database implementation
- `DuckDbDialect` - SQL generation for DuckDB
- File source registration (VIEW/TABLE creation)
- CRUD operations (INSERT, UPDATE, DELETE)
- Write-back engine (COPY TO export)
- Import pipeline (batched INSERT with conflict resolution)

## Data Flow

### Query Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  User Code                                                      │
│  db.Query<SalesRecord>().Where(x => x.Revenue > 10000)         │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  Jaunty.Fluent (Core)                                           │
│  Builds expression tree for WHERE clause                        │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDbDialect                                                  │
│  Translates expression tree to DuckDB SQL:                      │
│  WHERE "Revenue" > $1                                           │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDB.NET.Data                                                │
│  Executes SQL against in-memory DuckDB engine                   │
│  Returns DbDataReader                                           │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  Jaunty Materializer (Core)                                     │
│  DbDataReader → C# entity mapping                               │
│  Handles type conversion, nullables, [Column] attributes        │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  User Code                                                      │
│  List<SalesRecord> results                                      │
└─────────────────────────────────────────────────────────────────┘
```

### File Registration Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  FlatFileDatabase.Open("sales.csv")                             │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  CsvFileSource created                                          │
│  TableName: "sales"                                             │
│  FilePath: "/path/to/sales.csv"                                 │
│  GenerateReadFunction(): read_csv('/path/to/sales.csv', ...)   │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDbDialect.GenerateCreateViewSql()                          │
│  Returns: CREATE OR REPLACE VIEW "sales" AS                     │
│           SELECT * FROM read_csv('/path/to/sales.csv', ...)    │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDB executes CREATE VIEW                                    │
│  File is now queryable as table "sales"                         │
└─────────────────────────────────────────────────────────────────┘
```

### CRUD Flow (VIEW → TABLE Promotion)

```
┌─────────────────────────────────────────────────────────────────┐
│  db.InsertAsync(new SalesRecord { ... })                        │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDbFlatFileDatabase.EnsurePromotedToTableAsync()            │
│  Checks: Is source a VIEW?                                      │
│  If yes:                                                        │
│    1. CREATE TABLE sales_tmp AS SELECT * FROM sales            │
│    2. DROP VIEW sales                                          │
│    3. ALTER TABLE sales_tmp RENAME TO sales                    │
│  Marks source.IsPromotedToTable = true                          │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  INSERT INTO "sales" (...) VALUES (...)                         │
│  Executes against now-mutable TABLE                             │
└─────────────────────────────────────────────────────────────────┘
```

### Write-Back Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  db.SaveAsync<SalesRecord>(WriteBackMode.Overwrite)             │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDbDialect.GenerateCopyToSql()                              │
│  Returns: COPY "sales" TO 'temp.csv' (FORMAT CSV, HEADER true)  │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  DuckDB executes COPY TO                                        │
│  Writes modified data to temp file                              │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  Atomic replace:                                                │
│  1. Delete original file                                        │
│  2. Rename temp file to original path                           │
└─────────────────────────────────────────────────────────────────┘
```

### Import Pipeline Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  db.ImportIntoAsync<SalesRecord>(targetConnection)              │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  ImportDialectResolver.Resolve()                                │
│  Auto-detects target database from connection type:             │
│  - SqliteConnection → SqliteImportDialect                       │
│  - NpgsqlConnection → PostgreSqlImportDialect                   │
│  - SqlConnection → SqlServerImportDialect                       │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  TargetDdlGenerator.GenerateCreateTableSql() (if needed)        │
│  Creates target table from entity metadata                      │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│  ImportExecutor.ExecuteAsync()                                  │
│  1. SELECT * FROM "sales" (read from DuckDB)                    │
│  2. For each row:                                               │
│     - Map DuckDB types to CLR types                             │
│     - Handle DuckDBDateOnly → DateTime conversion               │
│  3. Batch INSERT into target database                           │
│  4. Apply conflict strategy (Error/Skip/Upsert)                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### 1. Two-Package Architecture

**Decision:** Split into `Jaunty.FlatFiles` (abstractions) and `Jaunty.FlatFiles.DuckDB` (engine).

**Rationale:**
- Abstractions package has zero DuckDB dependencies → NativeAOT compatible
- Engine package can evolve independently
- Future engines possible (e.g., `Jaunty.FlatFiles.Sqlite` using CSV virtual tables)

### 2. VIEW → TABLE Promotion

**Decision:** Files open as DuckDB VIEWs; first CRUD operation promotes to TABLE.

**Rationale:**
- VIEWs are lazy - no memory overhead for large files
- DuckDB can't UPDATE/DELETE through `read_csv_auto()` views
- Transparent to user - no explicit action needed
- Trade-off: First mutation has overhead of loading entire file

### 3. Expression-to-SQL Translation

**Decision:** Use lightweight `FlatFileExpressionHelper` instead of Jaunty.Fluent for CRUD.

**Rationale:**
- CRUD operations need simple predicate translation, not full query building
- Avoids dependency on Jaunty.Fluent for basic operations
- Supports common patterns: comparisons, AND/OR, string methods, IN clauses

### 4. Import Dialect Auto-Detection

**Decision:** Resolve `IImportDialect` from `DbConnection` type using contains matching.

**Rationale:**
- User doesn't need to specify dialect explicitly
- Custom dialects can be registered via `ImportDialectResolver.Register()`
- Fallback to SQLite dialect for unknown connection types

### 5. Positional Parameters ($1, $2, ...)

**Decision:** DuckDB uses 1-based positional parameters, not named parameters.

**Rationale:**
- DuckDB's native parameter style
- Requires careful parameter ordering in CRUD operations
- `paramOffset` parameter in `TranslatePredicate()` for complex scenarios

## Performance Considerations

### Memory Efficiency

- **Default (VIEW):** File not loaded into memory; DuckDB streams data on demand
- **PreloadIntoMemory (TABLE):** Entire file loaded once; faster for repeated queries
- **CRUD Promotion:** First mutation loads entire file into memory

### Query Pushdown

All filters, ordering, and pagination are pushed to DuckDB:
```csharp
// This generates a single SQL query with WHERE, ORDER BY, LIMIT
await db.Query<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .OrderByDescending(x => x.Date)
    .Take(50)
    .ToListAsync();
```

### Batch Operations

- **InsertAsync(IEnumerable<T>):** Single multi-row INSERT statement
- **Import Pipeline:** Configurable batch size within transaction
- **COPY TO:** DuckDB's native bulk export (much faster than row-by-row)

## Testing Strategy

| Test Category | Framework | Data | Mocks |
|--------------|-----------|------|-------|
| Abstractions | xUnit | N/A | None |
| DuckDB Integration | xUnit | Real DuckDB | None |
| Import Pipeline | xUnit | Real DuckDB + SQLite | None |
| Benchmarks | BenchmarkDotNet | Generated data | None |

**Principle:** No mocks for DuckDB - use real in-memory instances for accurate testing.

## Future Extensions

### Potential Additions

1. **Cloud Storage:** DuckDB's httpfs extension for S3/HTTP sources
2. **Glob Patterns:** `read_csv_auto('data/*.csv')` for multi-file sources
3. **Cross-Source JOINs:** Fluent API support for joining across file sources
4. **Streaming Import:** Row-by-row import for very large files
5. **Alternative Engines:** `Jaunty.FlatFiles.Sqlite` using SQLite's CSV virtual table

### Not Planned (v1)

- Real-time file watching / streaming ingestion
- Transaction semantics for flat file writes (best-effort only)
- ACID guarantees for write-back operations
