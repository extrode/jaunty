# Jaunty.FlatFiles

Flat file support for Jaunty, enabling SQL queries on CSV, TSV, Parquet, JSON, and other file formats using DuckDB as the embedded query engine.

---

## Overview

Jaunty.FlatFiles extends Jaunty core with the ability to:

- Query flat files using SQL
- Support for CSV, TSV, Parquet, JSON, Excel, Delta Lake, Iceberg
- Use DuckDB as an embedded query engine
- Import/export data to external databases
- Perform full CRUD operations on file data

---

## Documentation

### Architecture

- [`architecture.md`](architecture.md) - Architecture and design decisions
- [`code-analysis.md`](code-analysis.md) - Code analysis and audit results
- [`flatfile-spec.md`](flatfile-spec.md) - Original specification document

### API Reference

_API reference documentation coming soon_

Key API areas:
- `FlatFile.Open()` - Open flat file databases
- `IFlatFile` - Main database interface
- `IFileSource` - File source implementations
- File-specific sources: `CsvFileSource`, `ParquetFileSource`, etc.

---

## Quick Start

```csharp
using Jaunty.FlatFiles;
using Jaunty.FlatFiles.DuckDB;

// Open a CSV file
using var db = FlatFile.Open("data/sales.csv");

// Query using Jaunty's fluent API
var results = db.Connection
    .From<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .Select();

// Or use raw SQL
var records = await db.QueryAsync<SalesRecord>(
    "SELECT * FROM sales WHERE revenue > $1",
    new[] { (Name: "$1", Value: (object)10000) });
```

---

## Supported Formats

| Format | Extension | Read | Write | Import |
|--------|-----------|------|-------|--------|
| CSV | `.csv` | yes | yes | yes |
| TSV | `.tsv` | yes | yes | yes |
| Parquet | `.parquet` | yes | yes | yes |
| JSON | `.json`, `.ndjson` | yes | yes | yes |
| Excel | `.xlsx` | yes | yes | yes |
| Delta Lake | `.delta` | yes | no | yes |
| Iceberg | `.iceberg` | yes | no | yes |

---

## Architecture

### Key Components

1. **FlatFile** - Static factory for opening databases
2. **DuckDb** - Main database implementation
3. **DuckDbDialect** - SQL dialect for DuckDB
4. **File Sources** - CSV, TSV, Parquet, JSON implementations
5. **Import Pipeline** - Import to external databases

### Data Flow

```
Flat File → DuckDB View → SQL Query → DbDataReader → Jaunty Materialization → C# Objects
```

See [`architecture.md`](architecture.md) for detailed architecture documentation.

---

## Performance

Jaunty.FlatFiles is optimized for:

- **Lazy loading** - Files loaded as views, not tables
- **Query pushdown** - Filters executed by DuckDB
- **Minimal allocations** - Cached metadata and compiled delegates
- **NativeAOT compatible** - Full AOT support

See [`code-analysis.md`](code-analysis.md) for performance analysis.

---

## For Contributors

### Adding New File Formats

1. Implement `IFileSource` interface
2. Add to `FlatFile.CreateSourceFromExtension()`
3. Add tests in `tests/Jaunty.FlatFiles.DuckDB.Tests/`
4. Update this documentation

### Testing

```bash
# Run FlatFiles tests
dotnet test tests/Jaunty.FlatFiles.DuckDB.Tests

# Run with coverage
dotnet test /p:CollectCoverage=true
```

---

## See Also

| Document | Purpose |
|----------|---------|
| [`../../01-api-reference/`](../../01-api-reference/) | Jaunty core API |
| [`../../02-architecture/`](../../02-architecture/) | Jaunty core architecture |
| [`../README.md`](../README.md) | Extensions overview |

---

**Package**: `Extrode.Jaunty.FlatFiles.DuckDB`  
**Dependencies**: Jaunty core, DuckDB.NET.Data.Full  
**NativeAOT**: Fully compatible
