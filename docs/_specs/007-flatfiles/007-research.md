# Jaunty.FlatFiles — Research Notes

> research.md — Technical research findings to inform implementation.

---

## 1. DuckDB.NET ADO.NET Provider

**Package**: `DuckDB.NET.Data.Full`
**Repo**: https://github.com/Giorgi/DuckDB.NET
**Min TFM**: net6.0

### Connection

```csharp
// In-memory (ephemeral)
using var connection = new DuckDBConnection("DataSource=:memory:");
await connection.OpenAsync();

// File-backed (persistent)
using var connection = new DuckDBConnection("DataSource=cache.duckdb");
await connection.OpenAsync();
```

### Known Behaviors to Verify in M0

- **Parameter binding**: DuckDB.NET uses `$1`, `$2` positional parameters. Verify this works with Jaunty's parameter generation.
- **DbDataReader column ordinals**: Verify ordinal-based vs name-based access patterns.
- **NULL handling**: Verify `reader.IsDBNull()` behavior for various types.
- **Transaction support**: DuckDB supports transactions. Verify `BeginTransaction()`, `Commit()`, `Rollback()`.
- **Async support**: Verify `ExecuteReaderAsync()`, `ExecuteNonQueryAsync()` work correctly.
- **Type mapping edge cases**: `DateOnly`/`TimeOnly` (net8.0+), `decimal` precision, `Guid` as UUID.

### DuckDB SQL Reference Points

- `read_csv_auto()`: https://duckdb.org/docs/data/csv/overview
- `read_parquet()`: https://duckdb.org/docs/data/parquet/overview
- `read_json_auto()`: https://duckdb.org/docs/data/json/overview
- `COPY TO`: https://duckdb.org/docs/sql/statements/copy

## 2. File Format Capabilities

### CSV/TSV via `read_csv_auto()`

```sql
-- Auto-detection (delimiter, header, types, encoding)
SELECT * FROM read_csv_auto('file.csv');

-- Explicit configuration
SELECT * FROM read_csv_auto('file.csv',
    header=true,
    delim=',',
    nullstr='NA',
    skip=2,
    columns={'id': 'INTEGER', 'name': 'VARCHAR', 'amount': 'DECIMAL'}
);
```

Key capabilities:
- Auto-detects delimiter, header, column types, encoding, date formats
- Handles quoted fields, multiline values, various encodings
- `skip` parameter for metadata rows
- `nullstr` for custom NULL representations
- `columns` parameter for explicit schema override

### Parquet via `read_parquet()`

```sql
SELECT * FROM read_parquet('file.parquet');
SELECT * FROM read_parquet('file.parquet', hive_partitioning=true);
```

Key capabilities:
- Schema embedded in file metadata — no inference needed
- Columnar format = excellent query performance
- Predicate pushdown supported (DuckDB pushes WHERE to Parquet reader)
- Hive partitioning for directory-based partitions

### JSON via `read_json_auto()`

```sql
-- Auto-detect format
SELECT * FROM read_json_auto('file.json');

-- Explicit format
SELECT * FROM read_json_auto('file.json', format='newline_delimited');
SELECT * FROM read_json_auto('file.json', format='array');

-- With depth limit
SELECT * FROM read_json_auto('file.json', maximum_depth=3);
```

### COPY TO (Write-Back)

```sql
-- CSV output
COPY (SELECT * FROM sales) TO 'output.csv' (HEADER, DELIMITER ',');

-- Parquet output
COPY (SELECT * FROM sales) TO 'output.parquet' (FORMAT PARQUET);

-- JSON output
COPY (SELECT * FROM sales) TO 'output.json' (FORMAT JSON, ARRAY true);
```

## 3. VIEW vs TABLE Trade-offs

| Aspect | CREATE VIEW | CREATE TABLE AS |
|---|---|---|
| Memory | Minimal — reads from disk on query | Full dataset in DuckDB columnar storage |
| First query | Slightly slower (file I/O) | Faster (data already in memory) |
| Repeated queries | Re-reads file each time | Fast (columnar cache) |
| Mutations | NOT supported (read-only) | Full INSERT/UPDATE/DELETE |
| Use case | One-shot queries, large files | Repeated queries, CRUD operations |

**Strategy**: Default to VIEW. Promote to TABLE transparently on first mutation.

## 4. Bulk Import Target Strategies

### SQLite

- No native bulk API
- Best approach: batched INSERT within a single transaction
- Optimal batch size: ~5000 rows (diminishing returns above this)
- Use `BEGIN TRANSACTION` / `COMMIT` to batch

### PostgreSQL (Npgsql)

- `NpgsqlBinaryImporter` via `BeginBinaryImport()`
- Binary COPY protocol — fastest possible ingestion
- Requires explicit column type mapping

### SQL Server

- `SqlBulkCopy` class
- Columnar bulk insert
- Requires `SqlBulkCopyColumnMapping` for column alignment

## 5. NativeAOT Considerations

### Jaunty.FlatFiles (abstractions)

- No P/Invoke, no dynamic assembly loading
- All types are compile-time known
- Should be fully AOT-compatible with proper trimming annotations

### Jaunty.FlatFiles.DuckDB (engine)

- DuckDB.NET uses P/Invoke to call the native DuckDB C library
- P/Invoke generally works with NativeAOT
- Potential issues: any reflection-based deserialization in DuckDB.NET internals
- Mitigation: test with `PublishAot=true` in M0; document findings
