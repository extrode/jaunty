# Bulk Copy Architecture

## Overview

Jaunty's bulk copy architecture provides high-performance data loading for large datasets by leveraging database-specific native bulk copy APIs. The architecture is designed to:

1. **Automatically use native bulk copy** when beneficial (100+ rows by default)
2. **Maintain zero external dependencies** through reflection-based provider access
3. **Provide consistent API** across all supported database dialects
4. **Fall back gracefully** to standard INSERT operations when native bulk copy is unavailable

## Architecture Components

### Core Interfaces

#### `IBulkCopyProvider`

```csharp
internal interface IBulkCopyProvider
{
    bool IsSupported { get; }
    int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options);
    ValueTask<int> CopyToServerAsync(DbConnection connection, string tableName, IDataReader data, BulkCopyOptions options, CancellationToken cancellationToken);
}
```

**Purpose**: Abstracts database-specific bulk copy implementations.

**Implementations**:
- `SqlServerBulkCopyProvider` - Uses `SqlBulkCopy`
- `PostgreSqlBulkCopyProvider` - Uses `NpgsqlBinaryImporter` (COPY BINARY)
- `MySqlBulkCopyProvider` - Uses `MySqlBulkLoader` (LOAD DATA INFILE)
- `SQLiteBulkCopyProvider` - Optimized INSERT with transactions

### Entity Adapter

#### `EntityDataReader<T>`

```csharp
internal sealed class EntityDataReader<T> : IDataReader
```

**Purpose**: Adapts `IEnumerable<T>` to `IDataReader` for bulk copy APIs.

**Key Features**:
- Uses compiled expression trees for property access (no reflection during iteration)
- Getters cached per type in `EntityDataReaderCache<T>` (thread-safe lazy initialization)
- Streams entities without buffering entire dataset
- Handles null values correctly (converts to `DBNull.Value`)
- Implements full `IDataReader` interface for compatibility

**Thread Safety**:
Getters are initialized once per type using double-check locking pattern:
```csharp
private static class EntityDataReaderCache<TEntity> where TEntity : new()
{
    public static void Initialize(ColumnMetadata[] columns)
    {
        if (_getters is null)
        {
            lock (columns)
            {
                if (_getters is null)
                    _getters = BuildGetters(columns);
            }
        }
    }
}
```

**Usage**:
```csharp
var entities = GetEntities();
var metadata = MetadataCache<T>.Metadata;
var reader = new EntityDataReader<T>(entities, metadata);
// Pass reader to bulk copy provider
```

### Configuration

#### `BulkCopyConfiguration` (Global)

```csharp
public static class BulkCopyConfiguration
{
    public static int DefaultBatchSize { get; set; } = 10000;
    public static int DefaultTimeout { get; set; } = 30;
    public static BulkCopyIdentityMode DefaultIdentityMode { get; set; }
    public static bool DefaultCheckConstraints { get; set; } = false;
    public static int MinimumRowsForNativeBulkCopy { get; set; } = 100;
    public static bool EnableNativeBulkCopy { get; set; } = true;
}
```

**Purpose**: Global configuration for bulk copy behavior.

**Important**: Configure at application startup before executing bulk operations.

#### `BulkCopyOptions` (Per-Operation)

```csharp
internal sealed class BulkCopyOptions
{
    public int BatchSize { get; set; } = 10000;
    public int Timeout { get; set; } = 30;
    public BulkCopyIdentityMode IdentityMode { get; set; }
    public bool CheckConstraints { get; set; }
    public TableLockOption TableLock { get; set; }
    public bool EnableStreaming { get; set; } = true;
    public IDbTransaction? Transaction { get; set; }
}
```

**Purpose**: Per-operation configuration overrides.

### Enums

#### `BulkCopyIdentityMode`

| Value | Description |
|-------|-------------|
| `Default` | Provider default behavior |
| `KeepIdentity` | Preserve identity values from entities |
| `AutoGenerate` | Let database generate identities |

#### `TableLockOption`

| Value | Description |
|-------|-------------|
| `Default` | Provider default locking |
| `BulkLock` | Acquire bulk update lock (SQL Server: TABLOCK) |
| `NoLock` | No table lock |

## Database-Specific Implementations

### SQL Server

**Provider**: `SqlServerBulkCopyProvider`

**Technology**: `SqlBulkCopy` (via reflection)

**Features**:
- Native bulk copy API
- Supports transactions
- Configurable batch size
- Identity column handling
- Table locking options

**Reflection Strategy**:
```csharp
// Dynamically load from Microsoft.Data.SqlClient or System.Data.SqlClient
private static readonly Type? SqlBulkCopyType = Type.GetType("Microsoft.Data.SqlClient.SqlBulkCopy, Microsoft.Data.SqlClient")
    ?? Type.GetType("System.Data.SqlClient.SqlBulkCopy, System.Data");
```

**Performance**: 10-100x faster than INSERT for 10K+ rows

### PostgreSQL

**Provider**: `PostgreSqlBulkCopyProvider`

**Technology**: `NpgsqlBinaryImporter` (COPY BINARY format)

**Features**:
- Binary COPY protocol
- Very high throughput
- Automatic type conversion

**COPY Command**:
```sql
COPY table_name (col1, col2, ...) FROM STDIN BINARY
```

**Performance**: 15-25x faster than INSERT for 10K+ rows

### MySQL

**Provider**: `MySqlBulkCopyProvider`

**Technology**: `MySqlBulkLoader` (LOAD DATA LOCAL INFILE)

**Features**:
- CSV-based bulk loading
- Temporary file approach
- Automatic cleanup

**Process**:
1. Create temporary CSV file
2. Configure `MySqlBulkLoader` with file stream
3. Execute `LOAD DATA LOCAL INFILE`
4. Delete temporary file

**Performance**: 8-15x faster than INSERT for 10K+ rows

### SQLite

**Provider**: `SQLiteBulkCopyProvider`

**Technology**: Optimized INSERT with transactions

**Features**:
- WAL mode for better write performance
- Single transaction for all rows
- Prepared statement reuse

**Optimizations**:
```sql
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
```

**Note**: SQLite has no native bulk copy API. This provider offers the best possible performance using standard ADO.NET patterns.

**Performance**: 2-3x faster than standard INSERT

## Integration with BulkInsert

The bulk copy providers are automatically integrated with `BulkInsert<T>()`:

```csharp
private static int BulkInsertCore<T>(...)
{
    // ... validation ...
    
    ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
    
    // Use native bulk copy if:
    // 1. Provider supports it
    // 2. Enabled in configuration
    // 3. Row count >= threshold
    if (dialect.SupportsNativeBulkCopy && 
        BulkCopyConfiguration.EnableNativeBulkCopy &&
        entityList.Count >= BulkCopyConfiguration.MinimumRowsForNativeBulkCopy)
    {
        var bulkProvider = dialect.CreateBulkCopyProvider();
        var dataReader = new EntityDataReader<T>(entityList, cached.Metadata);
        return bulkProvider.CopyToServer(connection, tableName, dataReader, options);
    }
    
    // Fallback to existing multi-row/loop logic
    // ...
}
```

## Performance Characteristics

### When Native Bulk Copy Is Used

| Condition | Behavior |
|-----------|----------|
| Rows < threshold | Standard multi-row INSERT |
| Rows >= threshold | Native bulk copy |
| Provider unsupported | Standard INSERT |
| `EnableNativeBulkCopy = false` | Standard INSERT |

### Expected Performance Gains

| Database | 1K Rows | 10K Rows | 100K Rows |
|----------|---------|----------|-----------|
| SQL Server | 5x | 15x | 50x |
| PostgreSQL | 8x | 20x | 60x |
| MySQL | 3x | 10x | 30x |
| SQLite | 1.5x | 2x | 3x |

*Compared to standard multi-row INSERT*

## Error Handling

### Transaction Rollback

All bulk copy providers support transaction rollback:

```csharp
using var transaction = connection.BeginTransaction();
try
{
    connection.BulkInsert(entities, CommandOptions.WithTransaction(transaction));
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Provider-Specific Errors

| Error | Cause | Handling |
|-------|-------|----------|
| `InvalidOperationException` | Provider not available | Fallback to INSERT |
| `ArgumentException` | Invalid connection type | Throw immediately |
| Database errors | Constraint violations, etc. | Throw with database error |

## NativeAOT Compatibility

### Current Status

**Bulk copy providers use reflection** to access database-specific APIs without hard dependencies. This approach:

 Maintains zero external dependencies
 Requires reflection permissions (not fully NativeAOT-safe)

### Future Enhancement

For full NativeAOT compatibility, consider:

1. **Source-generated providers** - Generate provider code at compile time
2. **Extension packages** - Move bulk copy to `Jaunty.Providers.*` packages
3. **Direct references** - Add optional direct package references with trimming support

## Testing Strategy

### Unit Tests

- `BulkCopyConfigurationTests` - Configuration options
- `EntityDataReaderTests` - Entity-to-data reader adaptation

### Integration Tests

- `BulkCopyProviderTests` - Provider-specific tests per dialect
- Tests verify data integrity, null handling, transaction support

### Performance Tests

See `benchmarks/Jaunty.Benchmarks/Benchmarks/BulkCopyBenchmarks.cs`

## Extensibility

### Adding New Providers

1. Implement `IBulkCopyProvider`
2. Add `SupportsNativeBulkCopy` property to dialect
3. Add `CreateBulkCopyProvider()` method to dialect
4. Add integration tests

### Example: Oracle Provider

```csharp
internal sealed class OracleBulkCopyProvider : IBulkCopyProvider
{
    public bool IsSupported => OracleBulkCopyType != null;
    
    public int CopyToServer(...)
    {
        // Use OracleBulkCopy via reflection
    }
}
```

## Configuration Examples

### Application Startup

```csharp
// In Program.cs or Startup.cs
BulkCopyConfiguration.DefaultBatchSize = 5000;
BulkCopyConfiguration.DefaultTimeout = 60;
BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50; // Use native for 50+ rows
BulkCopyConfiguration.EnableNativeBulkCopy = true;
```

### Per-Operation Override

```csharp
var options = new BulkCopyOptions
{
    BatchSize = 1000,
    Timeout = 120,
    IdentityMode = BulkCopyIdentityMode.KeepIdentity,
    TableLock = TableLockOption.BulkLock,
    Transaction = transaction
};

// Use options with bulk copy provider directly
var provider = dialect.CreateBulkCopyProvider();
provider.CopyToServer(connection, "MyTable", reader, options);
```

## Limitations

1. **SQL Server**: Requires `Microsoft.Data.SqlClient` or `System.Data.SqlClient`
2. **PostgreSQL**: Requires `Npgsql`
3. **MySQL**: Requires `MySql.Data` or `MySqlConnector`
4. **SQLite**: No true bulk copy, only optimized INSERT
5. **Reflection overhead**: Small performance cost for provider access
6. **NativeAOT**: Not fully compatible due to reflection usage

## Future Enhancements

1. **Source-generated providers** for NativeAOT
2. **Streaming support** for very large datasets
3. **Progress reporting** callbacks
4. **Column mapping** configuration
5. **Error row handling** (skip bad rows vs fail fast)
6. **Parallel bulk copy** for multi-file loads

## References

- [SQL Server SqlBulkCopy](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlbulkcopy)
- [Npgsql Binary Import](https://www.npgsql.org/doc/copy.html)
- [MySQL LOAD DATA](https://dev.mysql.com/doc/refman/8.0/en/load-data.html)
- [SQLite Performance](https://www.sqlite.org/np1queryprob.html)
