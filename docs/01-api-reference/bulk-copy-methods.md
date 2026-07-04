# Bulk Copy Operations

## Overview

Jaunty's bulk copy functionality provides high-performance data loading for large datasets by automatically leveraging database-specific native bulk copy APIs (SqlBulkCopy, NpgsqlBinaryImporter, etc.).

**Key Benefits**:
- **10-100x faster** than standard INSERT for large datasets (10K+ rows)
- **Automatic activation** when beneficial (configurable threshold: 100 rows default)
- **Zero configuration required** - works out of the box
- **Consistent API** across all supported databases

## Quick Start

```csharp
using Jaunty;
using Jaunty.Configuration;

// Optional: Configure global settings at application startup
BulkCopyConfiguration.DefaultBatchSize = 10000;
BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;

// Bulk insert - native bulk copy used automatically for 100+ rows
var products = GetProducts(); // List<Product>
int inserted = connection.BulkInsert(products);

Console.WriteLine($"Inserted {inserted} products");
```

## Configuration

### Global Configuration

Configure at application startup:

```csharp
// Set default batch size
BulkCopyConfiguration.DefaultBatchSize = 10000;

// Set timeout (seconds)
BulkCopyConfiguration.DefaultTimeout = 30;

// Set minimum rows for native bulk copy (default: 100)
BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50;

// Disable native bulk copy entirely
BulkCopyConfiguration.EnableNativeBulkCopy = false;

// Configure identity handling
BulkCopyConfiguration.DefaultIdentityMode = BulkCopyIdentityMode.KeepIdentity;

// Reset to defaults
BulkCopyConfiguration.Reset();
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `DefaultBatchSize` | 10,000 | Rows per batch for native bulk copy |
| `DefaultTimeout` | 30 | Timeout in seconds (0 = no timeout) |
| `DefaultIdentityMode` | `Default` | How to handle identity columns |
| `DefaultCheckConstraints` | `false` | Whether to check constraints |
| `MinimumRowsForNativeBulkCopy` | 100 | Minimum rows to trigger native bulk copy |
| `EnableNativeBulkCopy` | `true` | Enable/disable native bulk copy |

### Identity Mode Options

| Mode | Description |
|------|-------------|
| `Default` | Provider default behavior |
| `KeepIdentity` | Preserve identity values from entities |
| `AutoGenerate` | Let database generate identity values |

### Table Lock Options

| Option | Description |
|--------|-------------|
| `Default` | Provider default locking |
| `BulkLock` | Acquire bulk update lock (SQL Server: TABLOCK) |
| `NoLock` | No table lock |

## API Reference

### BulkInsert<T>()

Inserts multiple entities using native bulk copy when beneficial.

```csharp
// Basic usage
int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();

// With options
int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();

// Ignore constraints (if supported)
int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();

int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
```

**Returns**: Number of rows inserted

**Example**:
```csharp
var products = new List<Product>
{
    new Product { Name = "Product 1", Price = 10.00m },
    new Product { Name = "Product 2", Price = 20.00m },
    // ... 100+ more items for native bulk copy
};

int inserted = connection.BulkInsert(products);
```

### BulkInsertAsync<T>()

Async version of BulkInsert.

```csharp
ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default);

ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default);
```

**Example**:
```csharp
int inserted = await connection.BulkInsertAsync(products, cancellationToken);
```

### BulkUpdate<T>()

Updates multiple entities in a single transaction.

```csharp
int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();

int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
```

**Note**: Bulk update uses standard UPDATE statements. Native bulk update is not generally supported across databases.

**Example**:
```csharp
var products = GetUpdatedProducts();
int updated = connection.BulkUpdate(products);
```

### BulkUpdateIgnoreConstraints<T>()

Updates multiple entities, bypassing foreign key constraints (if supported).

```csharp
int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();

int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
```

**Supported Databases**:
- PostgreSQL (`SET session_replication_role = 'replica'`)
- MySQL (`SET FOREIGN_KEY_CHECKS = 0`)
- SQLite (`PRAGMA foreign_keys = OFF`)
- SQL Server (not supported)

### BulkDelete<T>()

Deletes multiple entities by primary key.

```csharp
int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new();

int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new();
```

**Example**:
```csharp
var productsToDelete = GetProductsToDelete();
int deleted = connection.BulkDelete(productsToDelete);
```

## Transaction Support

All bulk operations support transactions:

```csharp
using var transaction = connection.BeginTransaction();

try
{
    connection.BulkInsert(products, CommandOptions.WithTransaction(transaction));
    connection.BulkInsert(orders, CommandOptions.WithTransaction(transaction));
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## Database-Specific Behavior

### SQL Server

**Technology**: `SqlBulkCopy`

**Performance**: 10-100x faster than INSERT for 10K+ rows

**Features**:
- Configurable batch size
- Identity column handling
- Table locking (TABLOCK)
- Transaction support

**Requirements**: `Microsoft.Data.SqlClient` or `System.Data.SqlClient`

### PostgreSQL

**Technology**: `NpgsqlBinaryImporter` (COPY BINARY)

**Performance**: 15-25x faster than INSERT for 10K+ rows

**Features**:
- Binary COPY protocol
- Very high throughput
- Automatic type conversion

**Requirements**: `Npgsql`

### MySQL / MariaDB

**Technology**: Chunked multi-row parameterized INSERT (2,000-parameter
budget per statement; no server-side `local_infile` requirement)

**Performance**: 12.9-16.1x faster than a transactional loop (measured 2026-07-04)

**Features**:
- No LOAD DATA / local_infile server configuration needed
- Reused prepared command for full chunks
- Own or caller-supplied transaction

**Requirements**: `MySql.Data` or `MySqlConnector`

### SQLite

**Technology**: Prepared-loop INSERT in a single transaction (BulkInsert
routes SQLite here automatically; multi-row VALUES is quadratic in
Microsoft.Data.Sqlite)

**Performance**: parity with hand-coded ADO.NET (measured 2026-07-04)

**Features**:
- Single transaction for all rows
- Prepared statement reuse

**Note**: SQLite has no native bulk copy API; there is no separate provider.

## Performance Guidelines

### When to Use Bulk Copy

| Scenario | Recommendation |
|----------|----------------|
| 1-10 rows | Use `Insert<T>()` |
| 10-100 rows | Use `BulkInsert<T>()` (standard INSERT) |
| 100+ rows | Use `BulkInsert<T>()` (native bulk copy) |
| 10K+ rows | Use `BulkInsert<T>()` (native bulk copy) |

### Optimization Tips

1. **Batch large datasets**: For 1M+ rows, batch into chunks of 10K-50K
2. **Use transactions**: Always wrap bulk operations in transactions
3. **Disable indexes**: For very large loads, consider dropping/recreating indexes
4. **Set appropriate batch size**: Match to your database's optimal batch size
5. **Consider identity mode**: Use `AutoGenerate` unless you need to preserve IDs

### Performance Comparison

| Rows | Standard INSERT | Native Bulk Copy | Speedup |
|------|----------------|------------------|---------|
| 100 | 50ms | 45ms | 1.1x |
| 1,000 | 500ms | 100ms | 5x |
| 10,000 | 5,000ms | 300ms | 16x |
| 100,000 | 50,000ms | 1,000ms | 50x |

*SQL Server example times*

## Error Handling

### Common Errors

**Parameter count mismatch**:
```
InvalidOperationException: No parameter binder found for type 'Product'.
```
**Solution**: Ensure source generation or reflection extension is used.

**Provider not available**:
```
InvalidOperationException: SqlBulkCopy is not available.
```
**Solution**: Ensure database provider package is installed.

**Constraint violation**:
```
DbException: FOREIGN KEY constraint failed.
```
**Solution**: Use `BulkInsertIgnoreConstraints<T>()` if appropriate.

### Transaction Rollback

Bulk operations automatically rollback on error when using transactions:

```csharp
using var transaction = connection.BeginTransaction();
try
{
    connection.BulkInsert(products, CommandOptions.WithTransaction(transaction));
    transaction.Commit();
}
catch (Exception ex)
{
    transaction.Rollback();
    // ex contains details of what failed
    throw;
}
```

## Entity Requirements

### Required Attributes

For bulk operations, entities should have:

```csharp
[Table("products")]
public class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("price")]
    public decimal Price { get; set; }
}
```

### Ignored Properties

Properties marked with `[Ignore]` are skipped:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    [Ignore]
    public string CalculatedValue { get; set; } // Not inserted
}
```

## Examples

### Basic Bulk Insert

```csharp
var products = new List<Product>
{
    new Product { Name = "Widget", Price = 9.99m },
    new Product { Name = "Gadget", Price = 19.99m },
    // ... 100+ more
};

int inserted = connection.BulkInsert(products);
Console.WriteLine($"Inserted {inserted} products");
```

### Bulk Insert with Transaction

```csharp
using var transaction = connection.BeginTransaction();
try
{
    var products = GetProducts();
    var categories = GetCategories();
    
    connection.BulkInsert(categories, CommandOptions.WithTransaction(transaction));
    connection.BulkInsert(products, CommandOptions.WithTransaction(transaction));
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Bulk Insert with Custom Options

```csharp
var options = new CommandOptions<Product>()
    .WithTimeout(60)
    .WithTransaction(transaction);

int inserted = connection.BulkInsert(products, options);
```

### Async Bulk Insert

```csharp
var products = await GetProductsAsync();
int inserted = await connection.BulkInsertAsync(products, cancellationToken);
```

### Bulk Update

```csharp
var products = GetUpdatedProducts();
int updated = connection.BulkUpdate(products);
Console.WriteLine($"Updated {updated} products");
```

### Bulk Delete

```csharp
var productsToDelete = GetProductsToDelete();
int deleted = connection.BulkDelete(productsToDelete);
Console.WriteLine($"Deleted {deleted} products");
```

## Troubleshooting

### Native Bulk Copy Not Being Used

**Symptom**: Performance is same as standard INSERT

**Check**:
1. Verify row count >= `MinimumRowsForNativeBulkCopy`
2. Verify `EnableNativeBulkCopy = true`
3. Check database provider is installed
4. Verify dialect supports native bulk copy

**Solution**:
```csharp
// Lower threshold
BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 50;

// Verify enabled
Console.WriteLine(BulkCopyConfiguration.EnableNativeBulkCopy); // Should be true
```

### Identity Values Not Preserved

**Symptom**: Identity columns get new values instead of entity values

**Solution**:
```csharp
BulkCopyConfiguration.DefaultIdentityMode = BulkCopyIdentityMode.KeepIdentity;
```

### Timeout Errors

**Symptom**: Bulk operation times out

**Solution**:
```csharp
BulkCopyConfiguration.DefaultTimeout = 120; // 2 minutes

// Or per-operation
var options = new CommandOptions<T>().WithTimeout(120);
```

## See Also

- [Bulk Copy Architecture](../02-architecture/bulk-copy-architecture.md)
- [Configuration](configuration.md)
- [Write Methods](write-methods.md)
