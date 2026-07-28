# Jaunty.FlatFiles — Data Model

> data-model.md — Entity definitions, interfaces, and type mappings.

---

## 1. Core Interfaces (Jaunty.FlatFiles package)

### IFlatFileDatabase

```csharp
public interface IFlatFileDatabase : IDisposable, IAsyncDisposable
{
    // Query
    IQueryable<T> Query<T>() where T : class, new();
    Task<List<T>> QueryAsync<T>(string rawSql, params object[] parameters) where T : class, new();

    // CRUD
    Task<int> InsertAsync<T>(T entity) where T : class, new();
    Task<int> InsertAsync<T>(IEnumerable<T> entities) where T : class, new();
    Task<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value) where T : class, new();
    Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();

    // Write-back
    Task SaveAsync<T>(string outputPath) where T : class, new();
    Task SaveAsync<T>(WriteBackMode mode) where T : class, new();
    Task ExportAsync<T>(string outputPath) where T : class, new();

    // Import
    Task ImportIntoAsync<T>(IDbConnection targetConnection, Action<ImportOptions> configure) where T : class, new();

    // Metadata
    FileSourceInfo GetSourceInfo<T>() where T : class, new();
    bool IsModified<T>() where T : class, new();
}
```

### IFileSource

```csharp
public interface IFileSource
{
    string FilePath { get; }
    FileFormat Format { get; }
    Type EntityType { get; }
    string TableName { get; }
    bool IsPreloaded { get; }
    bool IsPromotedToTable { get; }
}
```

### IFlatFileDialect

```csharp
public interface IFlatFileDialect : IDialect
{
    string GenerateCreateViewSql(IFileSource source);
    string GenerateCreateTableAsSql(IFileSource source);
    string GeneratePromoteToTableSql(IFileSource source);
    string GenerateCopyToSql(string tableName, string outputPath, ExportFormat format);
}
```

## 2. Configuration Types

### FlatFileDatabaseOptions

```csharp
public class FlatFileDatabaseOptions
{
    public List<IFileSourceRegistration> Sources { get; }
    public bool PreloadIntoMemory { get; set; } = false;
    public string? PersistPath { get; set; }

    public FlatFileDatabaseOptions AddCsv<T>(string path) where T : class, new();
    public FlatFileDatabaseOptions AddCsv<T>(string path, Action<CsvOptions> configure) where T : class, new();
    public FlatFileDatabaseOptions AddTsv<T>(string path) where T : class, new();
    public FlatFileDatabaseOptions AddTsv<T>(string path, Action<TsvOptions> configure) where T : class, new();
    public FlatFileDatabaseOptions AddParquet<T>(string path) where T : class, new();
    public FlatFileDatabaseOptions AddParquet<T>(string path, Action<ParquetOptions> configure) where T : class, new();
    public FlatFileDatabaseOptions AddJson<T>(string path) where T : class, new();
    public FlatFileDatabaseOptions AddJson<T>(string path, Action<JsonFileOptions> configure) where T : class, new();
    public FlatFileDatabaseOptions PersistTo(string duckdbPath);
}
```

### Per-Format Options

```csharp
public class CsvOptions
{
    public bool? HasHeader { get; set; }          // null = auto-detect
    public char? Delimiter { get; set; }           // null = auto-detect
    public char? QuoteChar { get; set; }           // null = auto-detect
    public string? NullString { get; set; }
    public int SkipRows { get; set; } = 0;
    public string? Encoding { get; set; }          // null = auto-detect
    public bool PreloadIntoMemory { get; set; } = false;
}

public class TsvOptions
{
    public bool? HasHeader { get; set; }
    public string? NullString { get; set; }
    public int SkipRows { get; set; } = 0;
    public string? Encoding { get; set; }
    public bool PreloadIntoMemory { get; set; } = false;
}

public class ParquetOptions
{
    public bool PreloadIntoMemory { get; set; } = false;
    public bool HivePartitioning { get; set; } = false;
}

public class JsonFileOptions
{
    public JsonFileFormat Format { get; set; } = JsonFileFormat.Auto;
    public int? MaxDepth { get; set; }
    public bool PreloadIntoMemory { get; set; } = false;
}

public enum JsonFileFormat { Auto, Array, NewlineDelimited }
```

### Write-Back Types

```csharp
public enum WriteBackMode
{
    Overwrite,       // Replace original file
    NewFile           // Requires output path
}

public enum ExportFormat
{
    Csv, Tsv, Parquet, Json
}

public class WriteBackOptions
{
    public WriteBackMode Mode { get; set; } = WriteBackMode.NewFile;
    public string? OutputPath { get; set; }
    public ExportFormat? OutputFormat { get; set; } // null = infer from extension
    public bool IncludeHeader { get; set; } = true;
    public char Delimiter { get; set; } = ',';
}
```

### Import Types

```csharp
public class ImportOptions
{
    public int BatchSize { get; set; } = 1000;
    public ConflictStrategy OnConflict { get; set; } = ConflictStrategy.Error;
    public bool CreateTableIfMissing { get; set; } = false;
    public Action<long, long?>? OnProgress { get; set; }
}

public enum ConflictStrategy
{
    Error,    // Throw on conflict
    Skip,     // Ignore conflicting rows
    Upsert    // Update on conflict
}
```

## 3. DuckDB ↔ C# Type Mapping

| C# Type | DuckDB Type | Notes |
|---|---|---|
| `string` | `VARCHAR` | |
| `int` | `INTEGER` | |
| `long` | `BIGINT` | |
| `short` | `SMALLINT` | |
| `byte` | `TINYINT` | |
| `float` | `FLOAT` | |
| `double` | `DOUBLE` | |
| `decimal` | `DECIMAL` | |
| `bool` | `BOOLEAN` | |
| `DateTime` | `TIMESTAMP` | |
| `DateTimeOffset` | `TIMESTAMPTZ` | |
| `DateOnly` | `DATE` | net8.0+ only |
| `TimeOnly` | `TIME` | net8.0+ only |
| `Guid` | `UUID` | |
| `byte[]` | `BLOB` | |
| `T?` (nullable) | Same + NULL | Nullable wrapper |

## 4. Test Entity Fixtures

```csharp
[Table("sales")]
public class SalesRecord
{
    [Key]
    public int Id { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = "";

    public decimal Revenue { get; set; }
    public int Quantity { get; set; }
    public DateTime Date { get; set; }

    [Column("region")]
    public string? Region { get; set; }
}

[Table("inventory")]
public class InventoryItem
{
    [Key]
    public int Sku { get; set; }
    public string Name { get; set; } = "";
    public int StockLevel { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}

[Table("customers")]
public class CustomerProfile
{
    [Key]
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
}
```
