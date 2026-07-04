# Jaunty

**The micro-ORM that respects your SQL and your time.**

Jaunty is a high-performance data access library for .NET that does one thing exceptionally well: execute your SQL and map results to objects. No query builders. No LINQ translation. No magic. Just fast, predictable, type-safe data access.

```csharp
var products = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @CategoryId",
    new { CategoryId = 1 });
```

---

## Why Jaunty Exists

Most micro-ORMs let you write SQL and get objects back. Jaunty does this too—but with deliberate design choices that catch bugs earlier and run faster.

**The core philosophy:**
- **Performant** — Compiled expression trees, not runtime reflection
- **Efficient** — Minimal allocations, cached metadata, zero dependencies
- **Elegant** — Clean API that reads like intent, not ceremony

### Strict by Default

When you call `Query<T>`, Jaunty requires that **every property** on your entity has a matching column in the result set. This is intentional.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// This throws immediately - missing 'Price' column
var products = connection.Query<Product>(
    "SELECT id, name FROM products");
// InvalidOperationException: "Strict mapping failed: property 'Price' has no matching column"
```

**Why?** Because silent partial mapping is a bug waiting to happen. If you wanted all three properties, you should know immediately that your query is wrong. If you intentionally want partial data, say so explicitly with `QueryPartial<T>`.

This catches mismatches at development time, not when a customer reports weird behavior in production.

---

## Installation

```bash
dotnet add package Beparey.Jaunty
```

Targets `netstandard2.0` and `net8.0`. Works with any ADO.NET provider.

---

## Quick Start

```csharp
using Jaunty;

// Strict mapping - all properties must have matching columns
var products = connection.Query<Product>(
    "SELECT id AS Id, name AS Name, price AS Price FROM products");

// Partial mapping - only map what exists
var summaries = connection.QueryPartial<ProductSummary>(
    "SELECT id AS Id, name AS Name FROM products");

// Scalar values
var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

// Parameters - named or positional
var filtered = connection.Query<Product>(sql, new { CategoryId = 1 });
var filtered = connection.Query<Product>(sql, 1);  // positional
var filtered = connection.Query<Product>(sql, 1, "active", 50.00m);  // multiple

// Async with cancellation
var products = await connection.QueryAsync<Product>(sql, cancellationToken);

// Insert, Update, Delete
var id = connection.Insert(product);        // Returns identity value
var rows = connection.Update(product);      // Returns rows affected
var rows = connection.Delete(product);      // Returns rows affected

// Bulk operations
var rows = connection.BulkInsert(products);   // Fast bulk insert
var rows = connection.BulkUpdate(products);   // Fast bulk update
var rows = connection.BulkDelete(products);   // Fast bulk delete

// Native bulk copy path engages automatically for 100+ rows
// Automatically enabled when Jaunty.Extensions.Reflection is loaded
JauntyReflectionExtensions.UseNativeBulkCopy();
var rows = connection.BulkInsert(largeProductList);  // Uses SqlBulkCopy, NpgsqlBinaryImporter, etc.

// Upsert (insert or update)
var rows = connection.Upsert(product);  // Inserts if new, updates if exists
```

---

## The Two Mapping Modes

### `Query<T>` — Strict Mode

Every entity property must have a corresponding column. Missing columns throw immediately.

```csharp
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
}

// All 3 columns required
var orders = connection.Query<Order>(
    "SELECT order_id AS OrderId, order_date AS OrderDate, total AS Total FROM orders");

// Missing 'Total' - throws InvalidOperationException
var orders = connection.Query<Order>(
    "SELECT order_id AS OrderId, order_date AS OrderDate FROM orders");
```

**Use strict mode when:** You expect complete entities. This is the default because it's the safer choice.

### `QueryPartial<T>` — Partial Mode

Map only the columns that exist. Unmatched properties keep their default values.

```csharp
public class OrderSummary
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
}

// Only selecting what we need
var summaries = connection.QueryPartial<OrderSummary>(
    "SELECT order_id AS OrderId, order_date AS OrderDate FROM orders");

// Extra columns in result? Ignored silently.
var summaries = connection.QueryPartial<OrderSummary>(
    "SELECT order_id AS OrderId, order_date AS OrderDate, total, customer_id FROM orders");
```

**Use partial mode when:** You're intentionally selecting a subset of columns, using DTOs, or working with projections.

---

## Complete API Reference

### Query Methods (Read Operations)

| Method | Returns | Mapping | Description |
|--------|---------|---------|-------------|
| `Query<T>()` | `List<T>` | Strict | All properties must have columns |
| `QueryPartial<T>()` | `List<T>` | Partial | Map only matching columns |
| `QueryFirst<T>()` | `T` | Strict | First row, throws if empty |
| `QueryFirstOrDefault<T>()` | `T?` | Strict | First row, null if empty |
| `QuerySingle<T>()` | `T` | Strict | Exactly one row, throws otherwise |
| `QuerySingleOrDefault<T>()` | `T?` | Strict | Single or null |
| `QueryScalar<T>()` | `T` | — | First column of first row |
| `QueryStream<T>()` | `IEnumerable<T>` | Strict | Streaming results |
| `QueryPartialStream<T>()` | `IEnumerable<T>` | Partial | Streaming partial results |
| `QueryMultiple()` | `GridReader` | — | Multiple result sets |

All methods have async counterparts (`QueryAsync<T>()`, etc.) with `CancellationToken` support.

### Write Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `Insert<T>()` | `long` | Insert entity, returns identity value |
| `Update<T>()` | `int` | Update entity by primary key |
| `Delete<T>()` | `int` | Delete entity by primary key |
| `Delete<T>(id)` | `int` | Delete by ID value |
| `BulkInsert<T>()` | `int` | Bulk insert multiple entities |
| `BulkUpdate<T>()` | `int` | Bulk update multiple entities |
| `BulkDelete<T>()` | `int` | Bulk delete multiple entities |
| `Upsert<T>()` | `int` | Insert or update by primary key |

All write methods have async counterparts.

### Stored Procedures

```csharp
// Execute stored procedure
var results = connection.ExecuteStoredProcedure<Product>("GetProductsByCategory", 
    new { CategoryId = 1 });

// With output parameters
var parameters = new SpParameters()
    .AddInput("CategoryId", 1)
    .AddOutput("TotalCount", DbType.Int32);

connection.ExecuteStoredProcedureWithOutput("GetProductCount", parameters);
var count = parameters.Get<int>("TotalCount");
```

---

## Parameter Binding

### Named Parameters

```csharp
var orders = connection.Query<Order>(
    "SELECT * FROM orders WHERE customer_id = @CustomerId AND status = @Status",
    new { CustomerId = "ALFKI", Status = "shipped" });
```

### Positional Parameters

Jaunty parses your SQL to extract parameter names, then binds values in order.

```csharp
// Single value
var order = connection.Query<Order>(
    "SELECT * FROM orders WHERE order_id = @Id",
    42);

// Multiple values
var orders = connection.Query<Order>(
    "SELECT * FROM orders WHERE customer_id = @Customer AND total > @MinTotal",
    "ALFKI", 100.00m);

// Array
var orders = connection.Query<Order>(sql, new object[] { "ALFKI", 100.00m });
```

### Duplicate Parameters

Same parameter used multiple times? Provide one value.

```csharp
// @Id appears twice, but we only pass it once
var results = connection.Query<Product>(
    "SELECT * FROM products WHERE category_id = @Id OR supplier_id = @Id",
    7);
```

### Parameter Count Validation

Jaunty validates immediately. No waiting for the database to complain.

```csharp
// SQL has 2 unique parameters, but we passed 3 values
connection.Query<Product>(sql, 1, 2, 3);
// ArgumentException: "Parameter count mismatch: SQL contains 2 unique parameter(s), but 3 value(s) provided."
```

---

## Transactions and Timeouts

Use `CommandOptions` to pass transaction or timeout settings. No overload confusion.

```csharp
using var transaction = connection.BeginTransaction();

// With transaction
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.WithTransaction(transaction));

// With timeout
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.WithTimeout(30));

// Both
var orders = connection.Query<Order>(sql, parameters,
    CommandOptions.With(transaction, timeoutSeconds: 30));

transaction.Commit();
```

### Bulk Operations with Transaction

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

---

## Attribute Mapping

Override table and column names per-entity.

```csharp
using Jaunty.Attributes;

[Table("order_items")]
public class OrderItem
{
    [Column("item_id")]
    public int Id { get; set; }

    [Column("product_name")]
    public string Name { get; set; }

    [Column("unit_price")]
    public decimal Price { get; set; }

    [Ignore]  // Not mapped from database
    public decimal CalculatedDiscount { get; set; }
    
    [Key]  // Primary key
    public int OrderId { get; set; }
    
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
}

// SQL uses database column names
var items = connection.QueryPartial<OrderItem>(
    "SELECT item_id, product_name, unit_price FROM order_items");
```

**Priority order:** `[Column]` attribute > `JauntyConfig.ColumnNameResolver` > Property name

---

## Logging and Diagnostics

Jaunty provides built-in command interception for logging, auditing, and custom diagnostics.

### LoggingInterceptor

Log SQL execution with configurable log levels, slow query detection, and parameter masking.

```csharp
using Microsoft.Extensions.Logging;
using Jaunty.Interceptors;
using Jaunty.Configuration;

// Create logger factory
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Configure logging
var loggingConfig = new LoggingConfiguration
{
    MinimumLogLevel = LogLevel.Information,
    SlowQueryThreshold = TimeSpan.FromSeconds(1),
    LogSql = true,
    LogParameters = true,
    SensitiveParameterNames = new HashSet<string> { "Password", "SSN", "CreditCard" }
};

// Create interceptor
var loggingInterceptor = new LoggingInterceptor(
    loggerFactory.CreateLogger<LoggingInterceptor>(),
    loggingConfig);

// Register with Jaunty
JauntyConfig.InterceptorPipeline = new InterceptorPipeline(new[] { loggingInterceptor });
```

**Configuration Options**:
- `MinimumLogLevel` - Minimum log level (default: `LogLevel.Information`)
- `SlowQueryThreshold` - Threshold for slow query warnings (default: `TimeSpan.Zero` = disabled)
- `LogSql` - Whether to log SQL command text (default: `true`)
- `LogParameters` - Whether to log parameter values (default: `true`)
- `SensitiveParameterNames` - Parameter names to mask in logs (default: empty)

### AuditInterceptor

Track command execution for compliance and troubleshooting without logging sensitive data.

```csharp
using Jaunty.Diagnostics;

var auditInterceptor = new AuditInterceptor(maxRecords: 1000);
JauntyConfig.InterceptorPipeline = new InterceptorPipeline(new[] { auditInterceptor });

// Get recent audit records
var recentCommands = auditInterceptor.GetRecentRecords(50);
foreach (var record in recentCommands)
{
    Console.WriteLine($"{record.Timestamp}: {record.Phase} - {record.CommandText}");
}

// Clear audit log
auditInterceptor.Clear();
```

**AuditRecord Properties**:
- `Timestamp` - UTC timestamp
- `Phase` - Executing, Executed, or Failed
- `CommandText` - SQL command text
- `CommandType` - Text, StoredProcedure, or TableDirect
- `Database` - Database name
- `ElapsedMilliseconds` - Execution duration
- `Success` - Whether execution succeeded
- `ExceptionType` / `ExceptionMessage` - Exception details on failure

### DiagnosticSource Integration

Jaunty emits events via `DiagnosticSource` for integration with OpenTelemetry, Application Insights, and other telemetry systems.

```csharp
using System.Diagnostics;
using Jaunty.Diagnostics;

// Subscribe to Jaunty events
var listener = new DiagnosticListener("Jaunty");
using var subscription = listener.Subscribe(new DiagnosticObserver());

// Or use the singleton instance
JauntyDiagnosticListener.Instance.Subscribe(new DiagnosticObserver());
```

**Event Names**:
- `Jaunty.Database.Command.Executing` - Before command execution
- `Jaunty.Database.Command.Executed` - After successful execution
- `Jaunty.Database.Command.Failed` - When execution fails

**Example Observer**:
```csharp
public class DiagnosticObserver : IObserver<KeyValuePair<string, object?>>
{
    public void OnNext(KeyValuePair<string, object?> evt)
    {
        switch (evt.Key)
        {
            case JauntyDiagnosticListener.CommandExecutingEventName:
                var executing = (CommandExecutingPayload)evt.Value!;
                Console.WriteLine($"Executing: {executing.CommandText}");
                break;

            case JauntyDiagnosticListener.CommandExecutedEventName:
                var executed = (CommandExecutedPayload)evt.Value!;
                Console.WriteLine($"Completed in {executed.ElapsedMilliseconds:F2}ms");
                break;

            case JauntyDiagnosticListener.CommandFailedEventName:
                var failed = (CommandFailedPayload)evt.Value!;
                Console.WriteLine($"Failed: {failed.ExceptionMessage}");
                break;
        }
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
```

### Custom Interceptors

Implement `ICommandInterceptor` for custom cross-cutting concerns.

```csharp
using Jaunty.Interceptors;

public class TimingInterceptor : ICommandInterceptor
{
    public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken ct)
    {
        // Record start time, add custom headers, etc.
        return new ValueTask();
    }

    public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken ct)
    {
        Console.WriteLine($"Query took {context.Elapsed.TotalMilliseconds:F2}ms");
        return new ValueTask();
    }

    public ValueTask OnCommandFailedAsync(CommandContext context, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"Query failed: {ex.Message}");
        return new ValueTask();
    }
}
```

**Interceptor Lifecycle**:
1. `OnCommandExecutingAsync` - Called before command execution
2. `OnCommandExecutedAsync` - Called after successful execution
3. `OnCommandFailedAsync` - Called when execution fails

### Dependency Injection

Register interceptors with `IServiceCollection`:

```csharp
using Jaunty;

var services = new ServiceCollection();

// Add logging
services.AddJauntyLogging(options =>
{
    options.MinimumLogLevel = LogLevel.Information;
    options.SlowQueryThreshold = TimeSpan.FromSeconds(1);
});

// Add custom interceptors
services.AddSingleton<TimingInterceptor>();
services.AddSingleton<ICommandInterceptor>(sp => sp.GetRequiredService<TimingInterceptor>());

// Apply interceptors after building the service provider
var serviceProvider = services.BuildServiceProvider();
serviceProvider.ApplyJauntyInterceptors();
```

---

## Global Configuration

Configure naming conventions once at application startup. Resolution is cached per-type for performance.

```csharp
using Jaunty.Configuration;

// In your startup/initialization code:
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
JauntyConfig.TableNameResolver = NamingConvention.SnakeCasePluralTable;
```

**Important:** Configure resolvers before executing any queries. Metadata is cached when a type is first used and won't pick up resolver changes afterward. This is intentional—it's faster, and configuration belongs at startup.

### Built-in Conventions

```csharp
NamingConvention.ToSnakeCase("ProductName")     // "product_name"
NamingConvention.ToLowerCase("ProductName")     // "productname"
NamingConvention.Pluralize("Category")          // "Categories"
NamingConvention.Pluralize("Company")           // "Companies"
NamingConvention.SnakeCasePluralTable           // Type -> "snake_case_plurals"
NamingConvention.SnakeCaseColumn                // Property -> "snake_case"
```

### Custom Resolvers

```csharp
JauntyConfig.TableNameResolver = type => $"tbl_{type.Name.ToLower()}";
JauntyConfig.ColumnNameResolver = prop => $"col_{prop.ToLower()}";
```

---

## Async Support

Every method has an async counterpart. `CancellationToken` is optional.

```csharp
// Query async
var products = await connection.QueryAsync<Product>(sql);
var products = await connection.QueryAsync<Product>(sql, cancellationToken);

// Write async
var id = await connection.InsertAsync(product);
var rows = await connection.BulkInsertAsync(products);
var rows = await connection.UpsertAsync(product);

// Streaming async
await foreach (var product in connection.QueryStreamAsync<Product>(sql, cancellationToken))
{
    Console.WriteLine($"{product.Id}: {product.Name}");
}

// Multiple result sets async
using var grid = await connection.QueryMultipleAsync(sql);
var products = grid.Read<Product>();
var categories = grid.Read<Category>();
```

---

## Multiple Result Sets

Execute multiple queries in a single round-trip.

```csharp
using var grid = connection.QueryMultiple(@"
    SELECT * FROM products WHERE category_id = @CategoryId;
    SELECT * FROM categories WHERE id = @CategoryId;
    SELECT COUNT(*) FROM products;
", new { CategoryId = 1 });

var products = grid.Read<Product>().ToList();
var category = grid.ReadFirst<Category>();
var totalProducts = grid.ReadScalar<int>();
```

---

## Streaming Large Result Sets

For large result sets, use streaming to avoid buffering everything in memory.

```csharp
// Synchronous streaming
foreach (var product in connection.QueryStream<Product>("SELECT * FROM products"))
{
    Process(product);
}

// Async streaming (.NET 8+)
await foreach (var product in connection.QueryStreamAsync<Product>("SELECT * FROM products"))
{
    await ProcessAsync(product);
}
```

---

## How It Works

### Connection Management

Jaunty respects your connection state:
- If the connection was closed, Jaunty opens it, executes, and closes it
- If the connection was already open, Jaunty leaves it open

No surprises. No leaked connections.

### Performance Architecture

1. **Compiled Setters** — Property setters are compiled via expression trees when a type is first used. No reflection during query execution.

2. **Metadata Caching** — Column mappings are cached in static generic classes (`MetadataCache<T>`). Zero allocation per query for metadata lookup.

3. **Parameter Caching** — Named parameter property getters are compiled and cached per anonymous type.

4. **SQL Parsing** — Parameter extraction skips string literals, comments, and quoted identifiers. Cheap compared to network roundtrip.

5. **Command Template Caching** — SQL parameter templates cached per query type.

6. **Bulk Copy Optimization** — Native bulk copy APIs (SqlBulkCopy, NpgsqlBinaryImporter, MySqlBulkLoader) automatically used for 100+ rows via `Jaunty.Extensions.Reflection`. The gain depends on provider and batch size; measurement status is tracked in [BENCHMARKS-2026-07-04.md](docs/05-quality/reports/BENCHMARKS-2026-07-04.md).

### NULL Handling

- **Nullable types** (`int?`, `string`, etc.): NULL becomes `default`
- **Non-nullable value types**: NULL throws `InvalidOperationException`

```csharp
public class Product
{
    public int Id { get; set; }           // NULL throws
    public int? CategoryId { get; set; }  // NULL becomes null
    public string Name { get; set; }      // NULL becomes null
}
```

---

## Design Decisions

### Why strict mapping by default?

Silent partial mapping causes bugs that surface far from their origin. Strict mode fails fast with a clear message. If you want partial data, `QueryPartial<T>` makes that intent explicit.

### Why parse SQL for positional parameters?

The cost of scanning a SQL string for `@param` tokens is negligible compared to network latency and query execution. The benefit—natural syntax like `Query(sql, 1, 2, 3)`—is worth it.

### Why `CommandOptions` instead of overloads?

Overloads for every combination of transaction/timeout/parameters create ambiguity. `Query(sql, 1, 30)` could mean "parameter 1, timeout 30" or "parameters 1 and 30". `CommandOptions` eliminates confusion.

### Why cache metadata in static constructors?

Performance. Static generic classes initialize once per type and live for the application lifetime. Configuration should happen at startup before queries run—this is a feature, not a limitation.

### Why separate Bulk operations?

Bulk operations use optimized paths for batch inserts/updates/deletes. They're faster than individual operations when working with collections.

### Native Bulk Copy Performance

For large datasets (100+ rows), Jaunty automatically uses native bulk copy APIs when `Jaunty.Extensions.Reflection` is loaded:

| Database | Native API | Advantage vs transactional loop |
|----------|-----------|------------------|
| SQL Server | `SqlBulkCopy` | 10-100x (vendor-reported, unverified*) |
| PostgreSQL | `NpgsqlBinaryImporter` (COPY) | **6.9-7.6x (measured 2026-07-04)** |
| MySQL/MariaDB | Chunked multi-row INSERT | **12.9-16.1x (measured 2026-07-04)** |
| SQLite | Prepared-loop INSERT (no bulk API exists) | parity with hand-coded ADO.NET (measured) |

\* Ranges describe the underlying native APIs' typical advantage over
row-by-row INSERTs as reported by their vendors; Jaunty-specific bulk
measurements are pending (PRD-002). Measured read-path comparisons
(vs ADO.NET, Dapper, EF Core, RepoDb, linq2db) are published in
[BENCHMARKS-2026-07-04.md](docs/05-quality/reports/BENCHMARKS-2026-07-04.md):
Jaunty is the lowest-allocating of the compared ORMs and competitive with
Dapper on throughput.

**Configuration**:
```csharp
using Jaunty.Configuration;

// Enable/disable native bulk copy (default: true)
BulkCopyConfiguration.EnableNativeBulkCopy = true;

// Set minimum rows to trigger native bulk copy (default: 100)
BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 100;

// Configure batch size (default: 10000)
BulkCopyConfiguration.DefaultBatchSize = 10000;

// Set timeout in seconds (default: 30)
BulkCopyConfiguration.DefaultTimeout = 30;
```

**Note**: Native bulk UPDATE and DELETE are not available in most database providers. Jaunty uses optimized standard SQL within transactions for these operations.

---

## Comparison

| Feature | Jaunty | Dapper | EF Core |
|---------|--------|--------|---------|
| Raw SQL execution | Yes | Yes | Yes |
| Strict mapping mode | Yes | No | No |
| Partial mapping mode | Yes | Yes | Yes |
| Positional parameters | Yes | No | No |
| Zero dependencies | Yes | Yes | No |
| Connection state management | Yes | Yes | Yes |
| Bulk operations | Yes | No | Yes |
| Upsert support | Yes | No | Yes |
| Streaming (IAsyncEnumerable) | Yes | No | Yes |
| Multiple result sets | Yes | Yes | Limited |
| Stored procedures | Yes | Yes | Yes |
| LINQ translation | No | No | Yes |
| Change tracking | No | No | Yes |

---

## Documentation

For more detailed documentation, see:

- [`CONTRIBUTING.md`](CONTRIBUTING.md) - Contributing guide
- [`docs/03-development/api-design-guidelines.md`](docs/03-development/api-design-guidelines.md) - API design guidelines
- [`docs/03-development/code-review-checklist.md`](docs/03-development/code-review-checklist.md) - Code review checklist
- [`docs/02-architecture/ARCHITECTURE-DECISIONS.md`](docs/02-architecture/ARCHITECTURE-DECISIONS.md) - Architecture decision records

---

## License

Private License

---

Built by [Syed Beparey](https://github.com/beparey)
