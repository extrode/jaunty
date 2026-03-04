# Jaunty.FlatFiles.DuckDB

**Jaunty.FlatFiles.DuckDB** is the DuckDB engine implementation for Jaunty.FlatFiles. It provides the underlying query engine that enables SQL queries on flat files (CSV, TSV, Parquet, JSON) using DuckDB's embedded database engine.

[![NuGet](https://img.shields.io/nuget/v/Beparey.Jaunty.FlatFiles.DuckDB.svg)](https://www.nuget.org/packages/Beparey.Jaunty.FlatFiles.DuckDB)

## Installation

```bash
dotnet add package Beparey.Jaunty.FlatFiles.DuckDB
```

This package depends on:
- `Beparey.Jaunty.FlatFiles` (abstractions)
- `DuckDB.NET.Data.Full` (DuckDB ADO.NET provider with native binaries)

## What's Included

This package provides:

- **DuckDbFlatFileDatabase** - The main database implementation backed by DuckDB
- **DuckDbDialect** - SQL dialect for DuckDB (identifier quoting, parameter binding, type mapping)
- **FlatFileDatabase** - Static factory for opening flat file databases
- **FlatFileImporter** - Convenience methods for importing flat files into databases
- **File source implementations** - CSV, TSV, Parquet, JSON readers

## How It Works

Jaunty.FlatFiles.DuckDB uses DuckDB as an embedded query engine:

1. **File Registration**: When you open a flat file, it's registered as a DuckDB VIEW using DuckDB's `read_csv_auto()`, `read_parquet()`, or `read_json_auto()` functions
2. **Query Translation**: Jaunty's fluent API (`.Where()`, `.OrderBy()`, etc.) is translated to DuckDB SQL
3. **Execution**: Queries execute against the in-memory DuckDB engine
4. **Materialization**: Results flow through Jaunty's existing materialization pipeline to C# entities

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────┐
│  Jaunty Fluent  │ ──► │  DuckDbDialect   │ ──► │  DuckDB SQL │
│     API         │     │  (SQL Generator) │     │             │
└─────────────────┘     └──────────────────┘     └──────┬──────┘
                                                        │
┌─────────────────┐     ┌──────────────────┐     ┌──────▼──────┐
│  C# Entities    │ ◄── │   Jaunty         │ ◄── │  DuckDB     │
│                 │     │  Materializer    │     │  Engine     │
└─────────────────┘     └──────────────────┘     └─────────────┘
```

## Usage

### Basic Query

```csharp
using Jaunty.FlatFiles.DuckDB;

[Table("sales")]
public class SalesRecord
{
    [Key]
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Revenue { get; set; }
    public DateTime Date { get; set; }
}

// Open a CSV file and query
using var db = FlatFileDatabase.Open("data/sales.csv");
var results = await db.Query<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .ToListAsync();
```

### Multi-Source Session

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("data/sales.csv");
    options.AddParquet<InventoryItem>("data/inventory.parquet");
    options.AddJson<CustomerProfile>("data/customers.json");
});

// Query each source
var sales = await db.Query<SalesRecord>().ToListAsync();
var inventory = await db.Query<InventoryItem>().ToListAsync();
```

### Import into Database

```csharp
using Microsoft.Data.Sqlite;

// Import CSV into SQLite
using var target = new SqliteConnection("Data Source=mydb.sqlite");
await FlatFileImporter.ImportAsync<SalesRecord>("data/sales.csv", target, import =>
{
    import.BatchSize = 5000;
    import.OnConflict = ConflictStrategy.Upsert;
    import.CreateTableIfMissing = true;
});
```

## DuckDB-Specific Behavior

### VIEW vs TABLE

Files are registered as **views** by default (lazy, read-only):

```sql
CREATE VIEW sales AS SELECT * FROM read_csv_auto('data/sales.csv');
```

On the first **CRUD operation** (INSERT/UPDATE/DELETE), the view is automatically promoted to a **table**:

```sql
CREATE TABLE sales AS SELECT * FROM read_csv_auto('data/sales.csv');
```

This is transparent to you - no explicit action needed.

### PreloadIntoMemory

For faster repeated queries, preload data into a table at registration:

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("data/sales.csv", csv =>
    {
        csv.PreloadIntoMemory = true;  // CREATE TABLE AS instead of CREATE VIEW
    });
});
```

### DuckDB Persistence

By default, DuckDB runs in-memory. Persist state to disk:

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.DatabasePath = "cache.duckdb";  // ":memory:" for in-memory
    options.AddCsv<SalesRecord>("data/sales.csv");
});
```

## Platform Support

This package uses `DuckDB.NET.Data.Full`, which includes native DuckDB binaries for:

- **Windows** (x64, x86, ARM64)
- **Linux** (x64, ARM64)
- **macOS** (x64, ARM64)

No separate DuckDB installation required.

### NativeAOT Compatibility

The `Jaunty.FlatFiles.DuckDB` package makes **best-effort** attempts at NativeAOT compatibility. However:

- The abstractions package (`Jaunty.FlatFiles`) is fully NativeAOT-compatible
- DuckDB.NET's AOT compatibility depends on the native binary loading mechanism
- Test thoroughly if deploying as a NativeAOT single-file executable

## SQL Dialect Features

The `DuckDbDialect` implements:

| Feature | DuckDB Implementation |
|---------|----------------------|
| Parameter prefix | `$1`, `$2` (positional, 1-based) |
| Identifier quoting | `"column_name"` (double quotes) |
| Boolean literals | `true` / `false` |
| LIMIT/OFFSET | `LIMIT n OFFSET m` |
| String concatenation | `\|\|` operator |
| Case-insensitive like | `ILIKE` |
| Upsert | `INSERT ... ON CONFLICT DO UPDATE` |
| Window functions | Full support |

## Type Mapping

| C# Type | DuckDB Type | Notes |
|---------|-------------|-------|
| `string` | `VARCHAR` | |
| `int` | `INTEGER` | |
| `long` | `BIGINT` | |
| `decimal` | `DECIMAL` | |
| `DateTime` | `TIMESTAMP` | Auto-cast from DATE columns |
| `DateTimeOffset` | `TIMESTAMPTZ` | |
| `bool` | `BOOLEAN` | |
| `Guid` | `UUID` | |
| `byte[]` | `BLOB` | |

## Known Limitations

1. **Single-writer constraint**: DuckDB allows multiple readers but only one writer. Write-back operations should not be concurrent.
2. **Schema inference**: DuckDB auto-detects column types from file data. Mismatches with entity types are validated at registration (if `ValidateSchema = true`).
3. **Large file mutations**: Promoting a large file from VIEW to TABLE loads the entire file into memory. Use caution with multi-GB files.
4. **Transaction semantics**: DuckDB supports transactions, but flat file write-back is best-effort (temp file + rename for atomicity).

## Troubleshooting

### "No file source registered for entity type"

You're querying an entity type that hasn't been registered. Ensure you called `AddCsv<T>()`, `AddParquet<T>()`, etc. before querying:

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("data/sales.csv");  // Register first
});
```

### "Schema validation failed"

The file's columns don't match your entity's property mappings. Check:
- Column names match (or use `[Column("name")]` attribute)
- Types are compatible (e.g., file has numeric data for `int` property)
- No missing columns

### DuckDB.NET native binary loading errors

Ensure you're using `DuckDB.NET.Data.Full` (includes native binaries). If you see platform-specific errors:
- Verify your platform is supported (Windows/Linux/macOS)
- Check for missing system dependencies (Linux: `libstdc++`, `zlib`)
- Try updating to the latest DuckDB.NET version

## Performance Tips

1. **Use views for large files** - Avoids loading entire file into memory
2. **Preload for repeated queries** - `PreloadIntoMemory = true` for small/medium files
3. **Filter early** - Use `.Where()` before `.ToListAsync()` to push filters to DuckDB
4. **Select only needed columns** - Use `.Select()` to reduce data transfer
5. **Batch CRUD operations** - Use `InsertAsync(IEnumerable<T>)` for bulk inserts

## See Also

- [Jaunty.FlatFiles README](../Jaunty.FlatFiles/README.md) - Abstractions package API
- [DuckDB Documentation](https://duckdb.org/docs/) - SQL reference and features
- [DuckDB.NET GitHub](https://github.com/Giorgi/DuckDB.NET) - ADO.NET provider details
