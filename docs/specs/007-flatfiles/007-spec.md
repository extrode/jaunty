# Jaunty.FlatFiles — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.

---

## 1. Problem Statement

C# developers working with flat file data (CSV, TSV, Parquet, JSON) face a fragmented tooling experience:

- **Reading** requires dedicated libraries (CsvHelper, Sep, Sylvan) with their own APIs
- **Querying** requires loading data into a database first, or writing LINQ-to-Objects against parsed rows
- **Importing** flat files into databases involves manual schema mapping and bulk insert boilerplate
- **Writing back** modifications to flat files requires re-serializing entire datasets

Jaunty already provides a fluent, high-performance API for querying relational databases. Extending this to flat files gives developers a single API surface for both structured databases and file-based data.

## 2. Vision

> "Jaunty gives you a unified fluent API over any data source — databases and files."

A developer should be able to open a CSV file and query it with the same Jaunty API they use for PostgreSQL, then import the results into SQLite, or modify rows and write them back — all without leaving the Jaunty ecosystem.

## 3. Target Users

- **Data engineers** who receive CSV/Parquet exports and need to query, filter, or transform them before loading into production databases
- **Application developers** who need to ingest flat file uploads (CSV imports, JSON configs, Parquet analytics) into their Jaunty-managed databases
- **Analysts** who want to use C# + Jaunty instead of Python/pandas for file-based data exploration

## 4. User Stories

### US-1: Query a CSV File
> As a developer, I want to open a CSV file and query it using Jaunty's fluent API, so that I don't need a separate CSV library or manual parsing.

**Acceptance Criteria:**
- Open a CSV file and execute `.Where()`, `.OrderBy()`, `.Take()`, `.Select()` queries
- Results materialize into strongly-typed C# entities
- Schema is inferred automatically or mapped via Jaunty's existing entity attributes
- Works with files up to 10GB without loading everything into memory

### US-2: Query Multiple File Formats
> As a developer, I want to query Parquet, TSV, and JSON files with the same API I use for CSV.

**Acceptance Criteria:**
- TSV, Parquet, and JSON (NDJSON + array) formats supported
- Same fluent API regardless of file format
- Format-specific configuration available (delimiter, encoding, JSON depth, etc.)
- Multiple file sources can be open in the same session

### US-3: Import Flat Files into a Database
> As a developer, I want to bulk import a CSV/Parquet file into my existing SQLite/PostgreSQL/SQL Server database using Jaunty's entity mapping.

**Acceptance Criteria:**
- Import uses Jaunty's entity class to define target schema
- Supports batch sizing and conflict resolution (skip, upsert, error)
- Uses target-specific bulk optimizations (SqlBulkCopy, Npgsql COPY, batched INSERT)
- Progress reporting for large imports
- Can create target table if it doesn't exist

### US-4: CRUD Operations Against Flat Files
> As a developer, I want to insert, update, and delete rows in a flat file and write the changes back.

**Acceptance Criteria:**
- INSERT new rows into a flat file source
- UPDATE existing rows by predicate
- DELETE rows by predicate
- Write changes back to the original file (destructive) or a new file (non-destructive)
- User explicitly chooses write-back mode — no silent overwrites
- Supports CSV, TSV, Parquet, and JSON output formats
- COPY TO for exporting query results to a new file

### US-5: Entity Mapping Compatibility
> As a developer, I want my existing Jaunty entity classes to work seamlessly with flat files.

**Acceptance Criteria:**
- `[Table]`, `[Column]`, `[Key]` attributes work for flat file sources
- Entity property types are validated against inferred file schema at registration
- Mismatches produce clear error messages (expected type X, got Y at column Z)

### US-6: Performant Large File Handling
> As a developer, I want to query large files (millions of rows) without excessive memory usage.

**Acceptance Criteria:**
- Default mode uses lazy evaluation (DuckDB VIEW, not TABLE)
- Opt-in preload mode for repeated queries on the same data
- Query execution pushes filters to DuckDB (no client-side filtering)
- Streaming result enumeration for large result sets

## 5. Out of Scope (v1)

- Real-time file watching / streaming ingestion
- Cloud storage (S3, Azure Blob) — future extension
- Cross-source JOINs in the fluent API (works at SQL level, not surfaced in v1)
- Transaction semantics for flat file writes (best-effort, not ACID)
- Glob patterns for multi-file sources (future extension)

## 6. Success Metrics

- All user stories pass acceptance criteria with automated tests
- Query performance within 2x of raw DuckDB SQL for equivalent operations
- Import pipeline within 1.5x of native bulk insert tools
- Zero Jaunty core regressions after extension is added to the solution
