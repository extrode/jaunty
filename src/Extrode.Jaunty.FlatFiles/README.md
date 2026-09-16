# Jaunty.FlatFiles

**Jaunty.FlatFiles** is an extension to the Jaunty micro-ORM that adds flat file (CSV, TSV, Parquet, JSON) query, CRUD, and import capabilities. It provides a unified fluent API for querying both databases and files using the same patterns.

[![NuGet](https://img.shields.io/nuget/v/Extrode.Jaunty.FlatFiles.svg)](https://www.nuget.org/packages/Extrode.Jaunty.FlatFiles)

## Features

- **Query flat files** using Jaunty's fluent API (`.Where()`, `.OrderBy()`, `.Take()`, `.Select()`)
- **Multiple formats**: CSV, TSV, Parquet, and JSON (array + newline-delimited)
- **Strong typing**: Results materialize into your C# entity classes
- **CRUD operations**: Insert, update, delete rows and write back to disk
- **Import pipeline**: Bulk import flat files into SQLite, PostgreSQL, or SQL Server
- **Lazy loading**: Files open as DuckDB views for memory-efficient querying
- **NativeAOT compatible**: Abstractions package is trim-safe

## Installation

```bash
dotnet add package Extrode.Jaunty.FlatFiles
```

You'll also need the DuckDB engine package:

```bash
dotnet add package Extrode.Jaunty.FlatFiles.DuckDB
```

## Quick Start

### Query a CSV File

```csharp
using Jaunty.FlatFiles;
using Jaunty.FlatFiles.DuckDB;

// Define your entity
[Table("sales")]
public class SalesRecord
{
    [Key]
    public int Id { get; set; }
    
    [Column("product_name")]
    public string ProductName { get; set; } = "";
    
    public decimal Revenue { get; set; }
    public DateTime Date { get; set; }
}

// Open and query
using var db = FlatFileDatabase.Open("data/sales.csv");
var results = await db.Query<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .OrderByDescending(x => x.Date)
    .Take(50)
    .ToListAsync();
```

### Configure Multiple Sources

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("data/sales.csv", csv =>
    {
        csv.HasHeader = true;
        csv.Delimiter = ',';
        csv.NullString = "NA";
    });

    options.AddParquet<InventoryItem>("data/inventory.parquet");
    
    options.AddJson<CustomerProfile>("data/customers.json", json =>
    {
        json.JsonFormat = JsonFileFormat.NewlineDelimited;
    });
});

// Query each source independently
var sales = await db.Query<SalesRecord>().Where(x => x.Revenue > 1000).ToListAsync();
var inventory = await db.Query<InventoryItem>().OrderBy(x => x.Name).ToListAsync();
```

### CRUD Operations

```csharp
using var db = FlatFileDatabase.Open("data/sales.csv");

// INSERT
await db.InsertAsync(new SalesRecord 
{ 
    Id = 999, 
    ProductName = "Widget", 
    Revenue = 50000, 
    Date = DateTime.Today 
});

// UPDATE
await db.UpdateAsync<SalesRecord>(
    x => x.Id == 42,
    x => x.Revenue, 
    75000);

// DELETE
await db.DeleteAsync<SalesRecord>(x => x.Revenue < 100);

// Write back to disk (non-destructive)
await db.SaveAsync<SalesRecord>("data/sales-modified.csv");

// Or overwrite the original file (destructive)
await db.SaveAsync<SalesRecord>(WriteBackMode.Overwrite);
```

### Import into Database

```csharp
using var source = FlatFileDatabase.Open("data/sales.csv");
using var target = new SqliteConnection("Data Source=mydb.sqlite");

await source.ImportIntoAsync<SalesRecord>(target, import =>
{
    import.BatchSize = 5000;
    import.OnConflict = ConflictStrategy.Upsert;
    import.CreateTableIfMissing = true;
    import.OnProgress = (imported, total) => 
        Console.WriteLine($"Imported {imported} rows");
});

// Or use the one-liner shorthand
await FlatFileImporter.ImportAsync<SalesRecord>("data/sales.csv", targetConnection);
```

## API Reference

### FlatFileDatabase.Open()

Opens a flat file database. Two overloads:

```csharp
// Single file - format inferred from extension
IFlatFileDatabase Open(string filePath);

// Configured multi-source
IFlatFileDatabase Open(Action<FlatFileDatabaseOptions> configure);
```

### FlatFileDatabaseOptions

Builder for configuring sources:

```csharp
options.AddCsv<T>(string path, Action<CsvFileSource>? configure)
options.AddTsv<T>(string path, Action<TsvFileSource>? configure)
options.AddParquet<T>(string path, Action<ParquetFileSource>? configure)
options.AddJson<T>(string path, Action<JsonFileSource>? configure)
options.PreloadIntoMemory = true;  // Load into TABLE instead of VIEW
options.DatabasePath = ":memory:"; // or path to persist DuckDB state
```

### Per-Format Options

**CsvFileSource:**
```csharp
csv.HasHeader = true;      // null = auto-detect
csv.Delimiter = ',';       // null = auto-detect
csv.QuoteChar = '"';       // null = auto-detect
csv.NullString = "NA";
csv.SkipRows = 0;
```

**ParquetFileSource:**
```csharp
parquet.HivePartitioning = false;
```

**JsonFileSource:**
```csharp
json.JsonFormat = JsonFileFormat.Auto;  // Auto, Array, or NewlineDelimited
json.MaxDepth = null;
```

### IFlatFileDatabase Interface

```csharp
interface IFlatFileDatabase
{
    // Query
    IQueryable<T> Query<T>() where T : class, new();
    
    // CRUD
    ValueTask<int> InsertAsync<T>(T entity, CancellationToken);
    ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CancellationToken);
    ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, 
                                   Expression<Func<T, object>> column, 
                                   object value, CancellationToken);
    ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken);
    
    // Write-back
    ValueTask SaveAsync<T>(string outputPath, CancellationToken);
    ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken);
    ValueTask ExportAsync<T>(string outputPath, CancellationToken);
    
    // Import
    ValueTask<long> ImportIntoAsync<T>(DbConnection target, 
                                        Action<ImportOptions>? configure, 
                                        CancellationToken);
    
    // Metadata
    IFileSource? GetSource<T>();
    bool IsModified<T>();
}
```

## Configuration Options

### PreloadIntoMemory

By default, files open as DuckDB **views** (lazy, read-only). Set `PreloadIntoMemory = true` to load data into a **table** at registration time:

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("data/sales.csv", csv =>
    {
        csv.PreloadIntoMemory = true;  // This source only
    });
    options.PreloadIntoMemory = true;  // All sources
});
```

**Views** are memory-efficient for large files and one-off queries. **Tables** are faster for repeated queries or when you need CRUD operations.

### DuckDB Persistence

By default, DuckDB runs in-memory. Persist state to disk for caching:

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.DatabasePath = "cache.duckdb";  // or ":memory:" for in-memory
    options.AddCsv<SalesRecord>("data/sales.csv");
});
```

## Entity Mapping

Jaunty.FlatFiles uses the same entity mapping as Jaunty core:

```csharp
[Table("sales")]  // Maps to DuckDB table/view name
public class SalesRecord
{
    [Key]                              // Primary key for upserts
    public int Id { get; set; }
    
    [Column("product_name")]           // Column name override
    public string ProductName { get; set; } = "";
    
    public decimal Revenue { get; set; }  // Property name used as column
}
```

## Supported Types

| C# Type | DuckDB Type |
|---------|-------------|
| `string` | `VARCHAR` |
| `int` | `INTEGER` |
| `long` | `BIGINT` |
| `short` | `SMALLINT` |
| `byte` | `TINYINT` |
| `float` | `FLOAT` |
| `double` | `DOUBLE` |
| `decimal` | `DECIMAL` |
| `bool` | `BOOLEAN` |
| `DateTime` | `TIMESTAMP` |
| `DateTimeOffset` | `TIMESTAMPTZ` |
| `Guid` | `UUID` |
| `byte[]` | `BLOB` |

Nullable types (`T?`) are supported.

## Error Handling

Schema mismatches produce descriptive errors:

```
Schema validation failed for 'sales': Entity property 'ProductName' 
maps to column 'product_name' which does not exist in the file.
Available columns: id, revenue, date, region
```

## Performance Tips

1. **Use views for large files** - Avoids loading entire file into memory
2. **Preload for repeated queries** - `PreloadIntoMemory = true` for small/medium files
3. **Push filters to DuckDB** - Use `.Where()` before `.ToListAsync()` to filter server-side
4. **Batch inserts** - Use `InsertAsync(IEnumerable<T>)` for bulk inserts
5. **Index for large tables** - After promoting to table, create indexes as needed

## See Also

- [Jaunty.FlatFiles.DuckDB README](../Jaunty.FlatFiles.DuckDB/README.md) - DuckDB engine details
- [Jaunty Documentation](../../docs/README.md) - Core Jaunty API reference
- [DuckDB SQL Reference](https://duckdb.org/docs/sql/introduction) - Underlying query engine
